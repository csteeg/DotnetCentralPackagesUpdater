using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Packaging;
using CentralNuGetUpdater.Models;
using System.Net;
using NuGet.Credentials;

namespace CentralNuGetUpdater.Services;

public class NuGetPackageService
{
    private readonly ILogger _logger;
    private ISettings _settings;
    private readonly List<SourceRepository> _sourceRepositories;
    private readonly ConsoleUIService _uiService;
    private readonly string? _configFilePath;

    public NuGetPackageService(string? configFilePath = null, ConsoleUIService? uiService = null)
    {
        _logger = NullLogger.Instance;
        _uiService = uiService ?? new ConsoleUIService();
        _configFilePath = configFilePath;

        // Load NuGet configuration
        _settings = LoadSettings(configFilePath);

        // Setup the same credential service that dotnet restore uses
        SetupCredentialService();

        // Get package sources from configuration
        var packageSourceProvider = new PackageSourceProvider(_settings);
        var packageSources = packageSourceProvider.LoadPackageSources().Where(s => s.IsEnabled);

        _sourceRepositories = packageSources.Select(source =>
            Repository.Factory.GetCoreV3(source)).ToList();

        if (!_sourceRepositories.Any())
        {
            // Fallback to nuget.org if no sources configured
            _sourceRepositories.Add(Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json"));
        }

        _uiService.DisplayInfo($"Loaded {_sourceRepositories.Count} package source(s)");
        foreach (var repo in _sourceRepositories)
        {
            _uiService.DisplayInfo($"  - {repo.PackageSource.Name}: {repo.PackageSource.Source}");
        }
    }

    private ISettings LoadSettings(string? configFilePath)
    {
        if (string.IsNullOrEmpty(configFilePath))
        {
            return Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
        }
        else
        {
            var configDirectory = Path.GetDirectoryName(configFilePath) ?? Directory.GetCurrentDirectory();
            var configFileName = Path.GetFileName(configFilePath);
            return Settings.LoadSpecificSettings(configDirectory, configFileName);
        }
    }

    private void SetupCredentialService()
    {
        try
        {
            // This sets up the same credential service that dotnet restore uses
            // It will discover and use the same credential providers (like Azure Artifacts Credential Provider)
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(_logger, nonInteractive: false);
            _uiService.DisplaySuccess("Credential service configured - will use same authentication as 'dotnet restore'");
        }
        catch (Exception ex)
        {
            _uiService.DisplayWarning($"Failed to setup credential service: {ex.Message}");
        }
    }

    public async Task<string?> GetLatestVersionAsync(string packageId, bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        foreach (var repository in _sourceRepositories)
        {
            try
            {
                var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
                var versions = await resource.GetAllVersionsAsync(packageId, new SourceCacheContext(), _logger, cancellationToken);

                if (versions?.Any() == true)
                {
                    var filteredVersions = includePrerelease
                        ? versions
                        : versions.Where(v => !v.IsPrerelease);

                    var latestVersion = filteredVersions.OrderByDescending(v => v).FirstOrDefault();
                    return latestVersion?.ToString();
                }
            }
            catch (Exception ex)
            {
                // With the credential service set up, authentication should be handled automatically
                _uiService.DisplayDebug($"Failed to check version for {packageId} from {repository.PackageSource.Name}: {ex.Message}");
                continue;
            }
        }

        return null;
    }

    public async Task<string?> GetLatestVersionForFrameworksAsync(string packageId, List<string> targetFrameworks, bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        if (!targetFrameworks.Any())
        {
            return await GetLatestVersionAsync(packageId, includePrerelease, cancellationToken);
        }

        var frameworks = targetFrameworks.Select(NuGetFramework.Parse).ToList();

        foreach (var repository in _sourceRepositories)
        {
            try
            {
                var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
                var packages = await metadataResource.GetMetadataAsync(packageId, includePrerelease, false,
                    new SourceCacheContext(), _logger, cancellationToken);

                if (packages?.Any() == true)
                {
                    var compatiblePackages = new List<IPackageSearchMetadata>();

                    // Use comprehensive compatibility checking based on package metadata
                    {
                        foreach (var package in packages.OrderByDescending(p => p.Identity.Version))
                        {
                            bool isCompatible = false;
                            var dependencySets = package.DependencySets?.ToList();

                            if (dependencySets != null && dependencySets.Any())
                            {
                                // Check if package supports any of our target frameworks using dependency sets
                                foreach (var framework in frameworks)
                                {
                                    var compatibleDependencySet = dependencySets.FirstOrDefault(ds =>
                                        ds.TargetFramework.Equals(NuGetFramework.AnyFramework) ||
                                        DefaultCompatibilityProvider.Instance.IsCompatible(framework, ds.TargetFramework));

                                    if (compatibleDependencySet != null)
                                    {
                                        isCompatible = true;
                                        break;
                                    }
                                }

                                // Additional check: if package doesn't support netstandard2.x but we target it, skip
                                var hasNetStandardTargets = frameworks.Any(f =>
                                    f.Framework.Equals(".NETStandard", StringComparison.OrdinalIgnoreCase) &&
                                    (f.Version.Major == 2 && f.Version.Minor <= 1));

                                if (hasNetStandardTargets && isCompatible)
                                {
                                    // Double-check that the package actually supports netstandard2.x
                                    var supportsNetStandard2x = dependencySets.Any(ds =>
                                        ds.TargetFramework.Framework.Equals(".NETStandard", StringComparison.OrdinalIgnoreCase) &&
                                        ds.TargetFramework.Version.Major == 2 && ds.TargetFramework.Version.Minor <= 1);

                                    if (!supportsNetStandard2x)
                                    {
                                        // Check if newer frameworks can satisfy this but not netstandard2.x
                                        var supportsNewerFrameworks = dependencySets.Any(ds =>
                                            (ds.TargetFramework.Framework.Equals(".NETCoreApp", StringComparison.OrdinalIgnoreCase) && ds.TargetFramework.Version.Major >= 6) ||
                                            (ds.TargetFramework.Framework.Equals(".NETStandard", StringComparison.OrdinalIgnoreCase) && ds.TargetFramework.Version.Major > 2));

                                        if (supportsNewerFrameworks)
                                        {
                                            _uiService.DisplayDebug($"Package {packageId} v{package.Identity.Version} supports newer frameworks but not netstandard2.x - skipping");
                                            isCompatible = false;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // If no dependency sets, be very conservative - don't assume compatibility
                                _uiService.DisplayDebug($"Package {packageId} v{package.Identity.Version} has no dependency information - being conservative");
                                isCompatible = false;
                            }

                            if (isCompatible)
                            {
                                compatiblePackages.Add(package);
                            }
                        }
                    }

                    var latestCompatible = compatiblePackages.FirstOrDefault();
                    if (latestCompatible != null)
                    {
                        return latestCompatible.Identity.Version.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _uiService.DisplayDebug($"Failed to check framework-specific version for {packageId} from {repository.PackageSource.Name}: {ex.Message}");
                continue;
            }
        }

        // Don't fallback to regular version check when using framework-aware checking
        // This prevents incompatible package updates
        _uiService.DisplayDebug($"No compatible version found for {packageId} with target frameworks: {string.Join(", ", targetFrameworks)}");
        return null;
    }



    public async Task<PackageInfo?> GetPackageInfoAsync(string packageId, bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        foreach (var repository in _sourceRepositories)
        {
            try
            {
                var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
                var packages = await metadataResource.GetMetadataAsync(packageId, includePrerelease, false,
                    new SourceCacheContext(), _logger, cancellationToken);

                var latestPackage = packages?.OrderByDescending(p => p.Identity.Version).FirstOrDefault();

                if (latestPackage != null)
                {
                    return new PackageInfo
                    {
                        Id = packageId,
                        LatestVersion = latestPackage.Identity.Version.ToString(),
                        Description = latestPackage.Description,
                        Published = latestPackage.Published?.DateTime
                    };
                }
            }
            catch (Exception ex)
            {
                // With the credential service set up, authentication should be handled automatically
                _uiService.DisplayDebug($"Failed to get metadata for {packageId} from {repository.PackageSource.Name}: {ex.Message}");
                continue;
            }
        }

        return null;
    }

    public async Task CheckForUpdatesAsync(List<PackageInfo> packages, bool includePrerelease = false, bool dryRun = false)
    {
        // Check if we have framework information
        var hasFrameworkInfo = packages.Any(p => p.TargetFrameworks.Any());
        if (hasFrameworkInfo)
        {
            var allFrameworks = packages.SelectMany(p => p.TargetFrameworks).Distinct().ToList();
            _uiService.DisplayInfo($"Using framework-aware updates for: {string.Join(", ", allFrameworks)}");
        }

        // Use progress display for better user experience
        await _uiService.DisplayProgressWithUpdatesAsync(
            $"Checking for updates",
            packages,
            async package =>
            {
                try
                {
                    // Add timeout for individual package processing
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                    string? latestVersion = null;

                    // Determine if we should include prerelease for this specific package
                    var packageIncludePrerelease = includePrerelease || IsCurrentVersionPrerelease(package.CurrentVersion);

                    // Use framework-aware checking based on applicable frameworks from conditions
                    if (package.ApplicableFrameworks.Any())
                    {
                        // For conditional packages, use only the frameworks they apply to
                        latestVersion = await GetLatestVersionForFrameworksAsync(package.Id, package.ApplicableFrameworks, packageIncludePrerelease, cts.Token);
                    }
                    else if (package.TargetFrameworks.Any())
                    {
                        // For non-conditional packages, use all target frameworks
                        latestVersion = await GetLatestVersionForFrameworksAsync(package.Id, package.TargetFrameworks, packageIncludePrerelease, cts.Token);
                    }
                    else
                    {
                        latestVersion = await GetLatestVersionAsync(package.Id, packageIncludePrerelease, cts.Token);
                    }

                    if (!string.IsNullOrEmpty(latestVersion))
                    {
                        package.LatestVersion = latestVersion;

                        // Get additional metadata (with timeout)
                        try
                        {
                            var packageInfo = await GetPackageInfoAsync(package.Id, packageIncludePrerelease, cts.Token);
                            if (packageInfo != null)
                            {
                                package.Description = packageInfo.Description;
                                package.Published = packageInfo.Published;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Metadata timeout - that's OK, we have the version
                            _uiService.DisplayDebug($"Metadata timeout for {package.Id}, but version found");
                        }
                    }
                    else
                    {
                        _uiService.DisplayDebug($"No version found for {package.Id}");
                    }
                }
                catch (OperationCanceledException)
                {
                    _uiService.DisplayWarning($"Timeout checking {package.Id} (30s limit)");
                }
                catch (Exception ex)
                {
                    _uiService.DisplayDebug($"Error checking updates for {package.Id}: {ex.Message}");
                }
            },
            package => package.Id); // Show package name in progress

        // Report packages that couldn't be resolved
        var packagesWithoutVersion = packages.Where(p => string.IsNullOrEmpty(p.LatestVersion)).ToList();
        if (packagesWithoutVersion.Any())
        {
            _uiService.DisplayWarning($"Could not resolve version for {packagesWithoutVersion.Count} package(s):");
            foreach (var pkg in packagesWithoutVersion)
            {
                _uiService.DisplayWarning($"  - {pkg.Id}");
            }
        }
    }

    private static bool IsCurrentVersionPrerelease(string version)
    {
        try
        {
            var nugetVersion = NuGetVersion.Parse(version);
            return nugetVersion.IsPrerelease;
        }
        catch
        {
            // If parsing fails, check for common prerelease indicators
            var lowerVersion = version.ToLowerInvariant();
            return lowerVersion.Contains("alpha") ||
                   lowerVersion.Contains("beta") ||
                   lowerVersion.Contains("rc") ||
                   lowerVersion.Contains("preview") ||
                   lowerVersion.Contains("pre") ||
                   lowerVersion.Contains("-");
        }
    }
}