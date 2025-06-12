using System.Xml.Linq;
using CentralNuGetUpdater.Models;

namespace CentralNuGetUpdater.Services;

public class CentralPackageMigrationService
{
    private readonly ConsoleUIService _uiService;

    public CentralPackageMigrationService(ConsoleUIService uiService)
    {
        _uiService = uiService;
    }

    public async Task<MigrationResult> MigrateToCentralPackageManagementAsync(string solutionPath, bool dryRun = false)
    {
        var result = new MigrationResult();

        try
        {
            _uiService.DisplayInfo("🔄 Starting migration to Central Package Management...");

            // Analyze solution to find all projects
            var solutionAnalyzer = new SolutionAnalyzerService();
            var solutionInfo = await AnalyzeSolutionAsync(solutionAnalyzer, solutionPath);

            if (!solutionInfo.Projects.Any())
            {
                result.ErrorMessage = "No projects found in solution";
                return result;
            }

            _uiService.DisplaySuccess($"Found {solutionInfo.Projects.Count} projects to analyze");

            // Extract all package references from project files
            var allPackages = new Dictionary<string, PackageReferenceInfo>();
            var projectModifications = new List<ProjectModification>();

            foreach (var project in solutionInfo.Projects)
            {
                var projectModification = await AnalyzeProjectAsync(project.ProjectPath, allPackages);
                if (projectModification.PackageReferences.Any())
                {
                    projectModifications.Add(projectModification);
                }
            }

            if (!allPackages.Any())
            {
                _uiService.DisplayWarning("No PackageReference items found in any project files");
                return result;
            }

            _uiService.DisplaySuccess($"Found {allPackages.Count} unique packages across all projects");

            // Create Directory.Packages.props path
            var directoryPackagesPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "Directory.Packages.props");

            result.DirectoryPackagesPath = directoryPackagesPath;
            result.PackagesFound = allPackages.Values.ToList();
            result.ProjectsToModify = projectModifications;

            if (dryRun)
            {
                _uiService.DisplayWarning("🔍 DRY RUN - No files will be modified");
                DisplayMigrationPreview(result);
                return result;
            }

            // Create or update Directory.Packages.props
            await CreateDirectoryPackagesPropsAsync(directoryPackagesPath, allPackages.Values);
            result.DirectoryPackagesCreated = true;

            // Update project files to remove version attributes
            foreach (var projectMod in projectModifications)
            {
                await UpdateProjectFileAsync(projectMod);
                result.ProjectFilesModified.Add(projectMod.ProjectPath);
            }

            _uiService.DisplaySuccess("✅ Migration completed successfully!");
            DisplayMigrationSummary(result);

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _uiService.DisplayError($"Migration failed: {ex.Message}");
            return result;
        }
    }

    private async Task<Models.SolutionInfo> AnalyzeSolutionAsync(SolutionAnalyzerService analyzer, string solutionPath)
    {
        if (File.Exists(solutionPath) && solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return await analyzer.AnalyzeSolutionAsync(solutionPath);
        }
        else
        {
            // Treat as directory path
            return await analyzer.AnalyzeDirectoryAsync(solutionPath);
        }
    }

    private async Task<ProjectModification> AnalyzeProjectAsync(string projectPath, Dictionary<string, PackageReferenceInfo> allPackages)
    {
        var modification = new ProjectModification { ProjectPath = projectPath };

        try
        {
            var projectContent = await File.ReadAllTextAsync(projectPath);
            var doc = XDocument.Parse(projectContent);

            // Find all PackageReference elements with Version attributes
            var packageReferences = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase))
                .Where(e => e.Attributes()
                    .Any(a => a.Name.LocalName.Equals("Include", StringComparison.OrdinalIgnoreCase)) &&
                           e.Attributes()
                    .Any(a => a.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var packageRef in packageReferences)
            {
                var includeAttr = packageRef.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName.Equals("Include", StringComparison.OrdinalIgnoreCase));
                var versionAttr = packageRef.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase));

                if (includeAttr?.Value != null && versionAttr?.Value != null)
                {
                    var packageId = includeAttr.Value;
                    var version = versionAttr.Value;

                    // Store package reference for this project
                    modification.PackageReferences.Add(new PackageReferenceInfo
                    {
                        PackageId = packageId,
                        Version = version,
                        Element = packageRef
                    });

                    // Add to global packages list (or update if version is newer)
                    if (!allPackages.ContainsKey(packageId))
                    {
                        allPackages[packageId] = new PackageReferenceInfo
                        {
                            PackageId = packageId,
                            Version = version
                        };
                    }
                    else
                    {
                        // Keep the highest version found across all projects
                        var currentVersion = allPackages[packageId].Version;
                        if (CompareVersions(version, currentVersion) > 0)
                        {
                            allPackages[packageId].Version = version;
                        }
                    }
                }
            }

            _uiService.DisplayInfo($"  📦 {Path.GetFileName(projectPath)}: {modification.PackageReferences.Count} package references");
        }
        catch (Exception ex)
        {
            _uiService.DisplayWarning($"Failed to analyze project {projectPath}: {ex.Message}");
        }

        return modification;
    }

    private async Task CreateDirectoryPackagesPropsAsync(string filePath, IEnumerable<PackageReferenceInfo> packages)
    {
        var doc = new XDocument(
            new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("ManagePackageVersionsCentrally", "true")
                ),
                new XElement("ItemGroup",
                    packages.OrderBy(p => p.PackageId).Select(p =>
                        new XElement("PackageVersion",
                            new XAttribute("Include", p.PackageId),
                            new XAttribute("Version", p.Version)
                        )
                    )
                )
            )
        );

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, doc.ToString());
        _uiService.DisplaySuccess($"📄 Created Directory.Packages.props with {packages.Count()} packages");
    }

    private async Task UpdateProjectFileAsync(ProjectModification projectMod)
    {
        try
        {
            var projectContent = await File.ReadAllTextAsync(projectMod.ProjectPath);
            var doc = XDocument.Parse(projectContent);

            bool modified = false;

            // Find the actual elements in the document and remove Version attributes
            var packageReferences = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase))
                .Where(e => e.Attributes()
                    .Any(a => a.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var packageRef in packageReferences)
            {
                var versionAttr = packageRef.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase));

                if (versionAttr != null)
                {
                    versionAttr.Remove();
                    modified = true;
                }
            }

            if (modified)
            {
                await File.WriteAllTextAsync(projectMod.ProjectPath, doc.ToString());
                _uiService.DisplaySuccess($"🔧 Updated {Path.GetFileName(projectMod.ProjectPath)} - removed {projectMod.PackageReferences.Count} version attributes");
            }
        }
        catch (Exception ex)
        {
            _uiService.DisplayError($"Failed to update project file {projectMod.ProjectPath}: {ex.Message}");
        }
    }

    private void DisplayMigrationPreview(MigrationResult result)
    {
        _uiService.DisplayInfo("\n📋 Migration Preview:");
        _uiService.DisplayInfo($"  📄 Directory.Packages.props: {result.DirectoryPackagesPath}");
        _uiService.DisplayInfo($"  📦 Packages to centralize: {result.PackagesFound.Count}");
        _uiService.DisplayInfo($"  🔧 Project files to modify: {result.ProjectsToModify.Count}");

        Console.WriteLine("\n📦 Packages that will be centralized:");
        foreach (var package in result.PackagesFound.OrderBy(p => p.PackageId))
        {
            Console.WriteLine($"  • {package.PackageId} → {package.Version}");
        }

        Console.WriteLine("\n🔧 Project files that will be modified:");
        foreach (var project in result.ProjectsToModify)
        {
            Console.WriteLine($"  • {Path.GetFileName(project.ProjectPath)} ({project.PackageReferences.Count} packages)");
        }
    }

    private void DisplayMigrationSummary(MigrationResult result)
    {
        _uiService.DisplayInfo("\n✅ Migration Summary:");
        _uiService.DisplaySuccess($"  📄 Created: {Path.GetFileName(result.DirectoryPackagesPath)}");
        _uiService.DisplaySuccess($"  📦 Centralized: {result.PackagesFound.Count} packages");
        _uiService.DisplaySuccess($"  🔧 Modified: {result.ProjectFilesModified.Count} project files");

        _uiService.DisplayInfo("\n🎯 Next Steps:");
        _uiService.DisplayInfo("  1. Test your solution builds correctly: dotnet build");
        _uiService.DisplayInfo("  2. Run package updates: cpup --path .");
        _uiService.DisplayInfo("  3. Commit your changes to version control");
    }

    private static int CompareVersions(string version1, string version2)
    {
        try
        {
            var v1 = new Version(version1);
            var v2 = new Version(version2);
            return v1.CompareTo(v2);
        }
        catch
        {
            // Fallback to string comparison if version parsing fails
            return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
        }
    }
}

public class MigrationResult
{
    public bool Success => string.IsNullOrEmpty(ErrorMessage);
    public string? ErrorMessage { get; set; }
    public string DirectoryPackagesPath { get; set; } = string.Empty;
    public bool DirectoryPackagesCreated { get; set; }
    public List<PackageReferenceInfo> PackagesFound { get; set; } = new();
    public List<ProjectModification> ProjectsToModify { get; set; } = new();
    public List<string> ProjectFilesModified { get; set; } = new();
}

public class PackageReferenceInfo
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public XElement Element { get; set; } = null!;
}

public class ProjectModification
{
    public string ProjectPath { get; set; } = string.Empty;
    public List<PackageReferenceInfo> PackageReferences { get; set; } = new();
}