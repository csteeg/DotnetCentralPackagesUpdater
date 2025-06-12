using System.Text.RegularExpressions;
using System.Xml.Linq;
using CentralNuGetUpdater.Models;

namespace CentralNuGetUpdater.Services;

public class SolutionAnalyzerService
{
    // Helper methods for case-insensitive XML parsing
    private static IEnumerable<XElement> GetDescendantsCaseInsensitive(XContainer container, string elementName)
    {
        return container.Descendants().Where(e => string.Equals(e.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase));
    }
    public async Task<SolutionInfo> AnalyzeSolutionAsync(string solutionPath)
    {
        var solutionInfo = new SolutionInfo { SolutionPath = solutionPath };

        if (!File.Exists(solutionPath))
        {
            return solutionInfo;
        }

        var solutionDir = Path.GetDirectoryName(solutionPath)!;

        // Load global properties from Directory.Build.props files
        var globalProperties = await LoadGlobalPropertiesAsync(solutionDir);

        var projectPaths = await ParseSolutionFileAsync(solutionPath);

        foreach (var projectPath in projectPaths)
        {
            var fullProjectPath = Path.IsPathRooted(projectPath)
                ? projectPath
                : Path.Combine(solutionDir, projectPath);

            if (File.Exists(fullProjectPath))
            {
                var projectInfo = await AnalyzeProjectAsync(fullProjectPath, globalProperties);
                solutionInfo.Projects.Add(projectInfo);
            }
        }

        return solutionInfo;
    }

    public async Task<SolutionInfo> AnalyzeDirectoryAsync(string directoryPath)
    {
        var solutionInfo = new SolutionInfo { SolutionPath = directoryPath };

        // Load global properties from Directory.Build.props files
        var globalProperties = await LoadGlobalPropertiesAsync(directoryPath);

        // Find all .csproj files recursively
        var projectFiles = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);

        foreach (var projectPath in projectFiles)
        {
            var projectInfo = await AnalyzeProjectAsync(projectPath, globalProperties);
            solutionInfo.Projects.Add(projectInfo);
        }

        return solutionInfo;
    }

    private async Task<List<string>> ParseSolutionFileAsync(string solutionPath)
    {
        var projectPaths = new List<string>();
        var content = await File.ReadAllTextAsync(solutionPath);

        // Parse project references from .sln file
        var projectRegex = new Regex(@"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+"",\s*""([^""]+\.csproj)"",", RegexOptions.IgnoreCase);
        var matches = projectRegex.Matches(content);

        foreach (Match match in matches)
        {
            var projectPath = match.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);
            projectPaths.Add(projectPath);
        }

        return projectPaths;
    }

    private async Task<ProjectInfo> AnalyzeProjectAsync(string projectPath, Dictionary<string, string>? globalProperties = null)
    {
        var projectInfo = new ProjectInfo
        {
            ProjectPath = projectPath,
            ProjectName = Path.GetFileNameWithoutExtension(projectPath)
        };

        try
        {
            var content = await File.ReadAllTextAsync(projectPath);
            var doc = XDocument.Parse(content);

            // Extract project-level properties first
            var projectProperties = ExtractProperties(doc);

            // Merge with global properties (global properties take precedence where not overridden)
            var combinedProperties = new Dictionary<string, string>();
            if (globalProperties != null)
            {
                foreach (var prop in globalProperties)
                {
                    combinedProperties[prop.Key] = prop.Value;
                }
            }
            foreach (var prop in projectProperties)
            {
                combinedProperties[prop.Key] = prop.Value; // Project properties override global
            }

            // Extract target frameworks with variable resolution
            var targetFrameworks = ExtractTargetFrameworks(doc, combinedProperties);
            projectInfo.TargetFrameworks.AddRange(targetFrameworks);

            // Store all properties for potential use
            foreach (var prop in combinedProperties)
            {
                projectInfo.Properties[prop.Key] = prop.Value;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not parse project {projectPath}: {ex.Message}");
        }

        return projectInfo;
    }

    private List<string> ExtractTargetFrameworks(XDocument doc, Dictionary<string, string>? properties = null)
    {
        var frameworks = new List<string>();

        // Look for TargetFramework (single) - case-insensitive
        var targetFramework = GetDescendantsCaseInsensitive(doc, "TargetFramework").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(targetFramework))
        {
            var resolvedFramework = ResolveVariables(targetFramework, properties);
            if (!string.IsNullOrEmpty(resolvedFramework))
            {
                frameworks.Add(resolvedFramework);
            }
        }

        // Look for TargetFrameworks (multiple, semicolon-separated) - case-insensitive
        var targetFrameworks = GetDescendantsCaseInsensitive(doc, "TargetFrameworks").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(targetFrameworks))
        {
            var resolvedFrameworks = ResolveVariables(targetFrameworks, properties);
            if (!string.IsNullOrEmpty(resolvedFrameworks))
            {
                frameworks.AddRange(resolvedFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrEmpty(f)));
            }
        }

        // If no frameworks were found in the project XML, fall back to checking global properties
        if (frameworks.Count == 0 && properties != null)
        {
            // Check for TargetFramework in global properties
            if (properties.TryGetValue("TargetFramework", out var globalTargetFramework)
                && !string.IsNullOrEmpty(globalTargetFramework))
            {
                var resolvedFramework = ResolveVariables(globalTargetFramework, properties);
                if (!string.IsNullOrEmpty(resolvedFramework))
                {
                    frameworks.Add(resolvedFramework);
                }
            }

            // Check for TargetFrameworks in global properties
            if (properties.TryGetValue("TargetFrameworks", out var globalTargetFrameworks)
                && !string.IsNullOrEmpty(globalTargetFrameworks))
            {
                var resolvedFrameworks = ResolveVariables(globalTargetFrameworks, properties);
                if (!string.IsNullOrEmpty(resolvedFrameworks))
                {
                    frameworks.AddRange(resolvedFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .Where(f => !string.IsNullOrEmpty(f)));
                }
            }
        }

        return frameworks.Distinct().ToList();
    }

    private Dictionary<string, string> ExtractProperties(XDocument doc)
    {
        var properties = new Dictionary<string, string>();

        foreach (var propertyGroup in GetDescendantsCaseInsensitive(doc, "PropertyGroup"))
        {
            foreach (var property in propertyGroup.Elements())
            {
                if (!string.IsNullOrEmpty(property.Value))
                {
                    properties[property.Name.LocalName] = property.Value;
                }
            }
        }

        return properties;
    }

    private async Task<Dictionary<string, string>> LoadGlobalPropertiesAsync(string startDirectory)
    {
        var globalProperties = new Dictionary<string, string>();

        // Walk up the directory tree looking for Directory.Build.props files
        var currentDir = startDirectory;
        while (!string.IsNullOrEmpty(currentDir))
        {
            var directoryBuildPropsPath = Path.Combine(currentDir, "Directory.Build.props");
            if (File.Exists(directoryBuildPropsPath))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(directoryBuildPropsPath);
                    var doc = XDocument.Parse(content);

                    // Extract properties from this Directory.Build.props
                    var properties = ExtractProperties(doc);
                    foreach (var prop in properties)
                    {
                        // Properties from closer Directory.Build.props files take precedence
                        if (!globalProperties.ContainsKey(prop.Key))
                        {
                            globalProperties[prop.Key] = prop.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not parse Directory.Build.props at {directoryBuildPropsPath}: {ex.Message}");
                }
            }

            // Move up one directory level
            var parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir == currentDir) // Reached root
                break;
            currentDir = parentDir;
        }

        return globalProperties;
    }

    private string ResolveVariables(string input, Dictionary<string, string>? properties)
    {
        if (string.IsNullOrEmpty(input) || properties == null || !input.Contains("$("))
        {
            return input;
        }

        var resolved = input;
        var variablePattern = @"\$\(([^)]+)\)";

        // Keep resolving until no more variables are found (handles nested variables)
        bool hasVariables;
        int maxIterations = 10; // Prevent infinite loops
        int iterations = 0;

        do
        {
            hasVariables = false;
            iterations++;

            foreach (Match match in Regex.Matches(resolved, variablePattern))
            {
                var variableName = match.Groups[1].Value;
                if (properties.TryGetValue(variableName, out var variableValue))
                {
                    resolved = resolved.Replace(match.Value, variableValue);
                    hasVariables = true;
                }
            }
        } while (hasVariables && iterations < maxIterations);

        return resolved;
    }
}