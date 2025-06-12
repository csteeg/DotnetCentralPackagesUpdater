using System.CommandLine;
using CentralNuGetUpdater.Services;

namespace CentralNuGetUpdater;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Central NuGet Package Updater - Check and update packages in Directory.Packages.props");

        // Update command (default)
        var pathOption = new Option<string>(
            name: "--path",
            description: "Path to Directory.Packages.props file or the directory containing it",
            getDefaultValue: () => Directory.GetCurrentDirectory());
        pathOption.AddAlias("-p");

        var configOption = new Option<string?>(
            name: "--config",
            description: "Path to nuget.config file (optional)");
        configOption.AddAlias("-c");

        var prereleaseOption = new Option<bool>(
            name: "--prerelease",
            description: "Include prerelease versions when checking for updates",
            getDefaultValue: () => false);
        prereleaseOption.AddAlias("--pre");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Show what would be updated without making changes",
            getDefaultValue: () => false);
        dryRunOption.AddAlias("-d");

        var disableFrameworkCheckOption = new Option<bool>(
            name: "--disable-framework-check",
            description: "Disable framework-aware checking (useful for analyzer packages)",
            getDefaultValue: () => false);
        disableFrameworkCheckOption.AddAlias("--no-framework-check");

        // Migration command
        var migrateCommand = new Command("migrate", "Migrate a solution from regular PackageReference to Central Package Management");

        var migrateSolutionOption = new Option<string>(
            name: "--solution",
            description: "Path to solution file (.sln) or directory containing projects",
            getDefaultValue: () => Directory.GetCurrentDirectory());
        migrateSolutionOption.AddAlias("-s");

        var migrateDryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Preview migration changes without modifying files",
            getDefaultValue: () => false);
        migrateDryRunOption.AddAlias("-d");

        migrateCommand.AddOption(migrateSolutionOption);
        migrateCommand.AddOption(migrateDryRunOption);

        migrateCommand.SetHandler(async (solutionPath, dryRun) =>
        {
            await RunMigration(solutionPath, dryRun);
        }, migrateSolutionOption, migrateDryRunOption);

        rootCommand.AddOption(pathOption);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(prereleaseOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(disableFrameworkCheckOption);
        rootCommand.AddCommand(migrateCommand);

        rootCommand.SetHandler(async (path, configPath, includePrerelease, dryRun, disableFrameworkCheck) =>
        {
            await RunUpdater(path, configPath, includePrerelease, dryRun, disableFrameworkCheck);
        }, pathOption, configOption, prereleaseOption, dryRunOption, disableFrameworkCheckOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunUpdater(string path, string? configPath, bool includePrerelease, bool dryRun, bool disableFrameworkCheck)
    {
        var ui = new ConsoleUIService();
        ui.DisplayWelcome();

        try
        {
            // Find Directory.Packages.props file
            string directoryPackagesPath;
            if (File.Exists(path) && Path.GetFileName(path).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase))
            {
                // User provided the full path to the file
                directoryPackagesPath = path;
                path = Path.GetDirectoryName(path)!; // Update path to be the directory for auto-config detection
            }
            else
            {
                // User provided a directory path
                directoryPackagesPath = Path.Combine(path, "Directory.Packages.props");
            }

            if (!File.Exists(directoryPackagesPath))
            {
                ui.DisplayError($"Directory.Packages.props not found at: {directoryPackagesPath}");
                return;
            }

            // Auto-detect nuget.config if not explicitly specified
            if (string.IsNullOrEmpty(configPath))
            {
                var autoDetectedConfig = Path.Combine(path, "nuget.config");
                if (File.Exists(autoDetectedConfig))
                {
                    configPath = autoDetectedConfig;
                }
            }

            // Analyze solution for target frameworks
            ui.DisplayProgress("Analyzing solution and projects...");
            var solutionAnalyzer = new SolutionAnalyzerService();
            var solutionInfo = await AnalyzeSolutionInDirectoryAsync(solutionAnalyzer, path, ui);

            // Parse packages from Directory.Packages.props
            ui.DisplayProgress("Parsing Directory.Packages.props...");
            var parser = new DirectoryPackagesParser();
            var packages = await parser.ParseDirectoryPackagesAsync(directoryPackagesPath, solutionInfo);

            if (!packages.Any())
            {
                ui.DisplayWarning("No packages found in Directory.Packages.props");
                return;
            }

            ui.DisplaySuccess($"Found {packages.Count} packages");

            // Initialize NuGet service with config
            ui.DisplayProgress("Initializing NuGet service...");
            var nugetService = new NuGetPackageService(configPath, ui);

            // Check for updates
            ui.DisplayProgress("Checking for package updates...");
            await nugetService.CheckForUpdatesAsync(packages, includePrerelease, dryRun, disableFrameworkCheck);

            // Display results
            ui.DisplayPackages(packages);

            var packagesWithUpdates = packages.Where(p => p.HasUpdate).ToList();
            if (!packagesWithUpdates.Any())
            {
                ui.DisplaySuccess("All packages are up to date!");
                return;
            }

            if (dryRun)
            {
                ui.DisplayWarning("Dry run mode - no changes will be made");
                ui.DisplaySuccess($"Found {packagesWithUpdates.Count} packages that could be updated");
                return;
            }

            // Let user select packages to update
            var selectedPackages = ui.SelectPackagesToUpdate(packages);

            if (!selectedPackages.Any())
            {
                ui.DisplayWarning("No packages selected for update");
                return;
            }

            // Confirm updates
            if (!ui.ConfirmUpdate(selectedPackages))
            {
                ui.DisplayWarning("Update cancelled by user");
                return;
            }

            // Apply updates
            ui.DisplayProgress("Updating Directory.Packages.props...");
            await parser.UpdateDirectoryPackagesAsync(directoryPackagesPath, packages);

            ui.DisplaySuccess($"Successfully updated {selectedPackages.Count} packages!");

            // Display summary
            Console.WriteLine();
            ui.DisplaySuccess("Updated packages:");
            foreach (var package in selectedPackages)
            {
                Console.WriteLine($"  • {package.Id}: {package.CurrentVersion} → {package.LatestVersion}");
            }
        }
        catch (Exception ex)
        {
            ui.DisplayError($"An error occurred: {ex.Message}");

            if (ex.InnerException != null)
            {
                ui.DisplayError($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    static async Task<Models.SolutionInfo> AnalyzeSolutionInDirectoryAsync(SolutionAnalyzerService analyzer, string directoryPath, ConsoleUIService ui)
    {
        // First try to find a .sln file
        var solutionFiles = Directory.GetFiles(directoryPath, "*.sln", SearchOption.TopDirectoryOnly);

        if (solutionFiles.Length == 1)
        {
            ui.DisplayInfo($"Found solution file: {Path.GetFileName(solutionFiles[0])}");
            var solutionInfo = await analyzer.AnalyzeSolutionAsync(solutionFiles[0]);

            if (solutionInfo.Projects.Any())
            {
                var frameworks = string.Join(", ", solutionInfo.AllTargetFrameworks);
                ui.DisplaySuccess($"Analyzed {solutionInfo.Projects.Count} projects targeting: {frameworks}");
                return solutionInfo;
            }
        }
        else if (solutionFiles.Length > 1)
        {
            ui.DisplayWarning($"Multiple solution files found, analyzing directory instead");
        }

        // Fallback to analyzing directory for .csproj files
        ui.DisplayInfo("Analyzing all .csproj files in directory...");
        var directoryInfo = await analyzer.AnalyzeDirectoryAsync(directoryPath);

        if (directoryInfo.Projects.Any())
        {
            var frameworks = string.Join(", ", directoryInfo.AllTargetFrameworks);
            ui.DisplaySuccess($"Analyzed {directoryInfo.Projects.Count} projects targeting: {frameworks}");
        }
        else
        {
            ui.DisplayWarning("No projects found for target framework analysis");
        }

        return directoryInfo;
    }

    static async Task RunMigration(string solutionPath, bool dryRun)
    {
        var ui = new ConsoleUIService();
        ui.DisplayWelcome();

        try
        {
            var migrationService = new CentralPackageMigrationService(ui);
            var result = await migrationService.MigrateToCentralPackageManagementAsync(solutionPath, dryRun);

            if (!result.Success)
            {
                ui.DisplayError($"Migration failed: {result.ErrorMessage}");
                return;
            }

            if (dryRun)
            {
                ui.DisplayInfo("\n💡 To perform the actual migration, run the command again without --dry-run");
            }
        }
        catch (Exception ex)
        {
            ui.DisplayError($"An error occurred during migration: {ex.Message}");

            if (ex.InnerException != null)
            {
                ui.DisplayError($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }
}