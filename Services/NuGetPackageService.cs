using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
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

                    // For framework-specific packages, prioritize framework-aligned versions
                    if (IsFrameworkSpecificPackage(packageId))
                    {
                        // Group versions by major version
                        var versionsByMajor = packages.GroupBy(p => p.Identity.Version.Major).OrderByDescending(g => g.Key);

                        foreach (var targetFramework in targetFrameworks)
                        {
                            var frameworkMajorVersion = GetFrameworkMajorVersion(targetFramework);
                            if (frameworkMajorVersion.HasValue)
                            {
                                // Find the latest version that matches the framework major version
                                var matchingMajorGroup = versionsByMajor.FirstOrDefault(g => g.Key == frameworkMajorVersion.Value);
                                if (matchingMajorGroup != null)
                                {
                                    var latestInGroup = matchingMajorGroup.OrderByDescending(p => p.Identity.Version).First();
                                    if (!compatiblePackages.Contains(latestInGroup))
                                    {
                                        compatiblePackages.Add(latestInGroup);
                                    }
                                    break; // Found framework-appropriate version
                                }
                            }
                        }
                    }

                    // If no framework-specific versions found, or package is not framework-specific, use regular compatibility checking
                    if (!compatiblePackages.Any())
                    {
                        foreach (var package in packages.OrderByDescending(p => p.Identity.Version))
                        {
                            var dependencySets = package.DependencySets?.ToList();

                            // If no dependency sets, assume it's compatible with all frameworks
                            if (dependencySets == null || !dependencySets.Any())
                            {
                                compatiblePackages.Add(package);
                                continue;
                            }

                            // Check if package supports any of our target frameworks
                            bool isCompatible = false;
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

        // Fallback to regular version check
        return await GetLatestVersionAsync(packageId, includePrerelease, cancellationToken);
    }

    private int? GetFrameworkMajorVersion(string targetFramework)
    {
        // Extract major version from frameworks like "net8.0", "net9.0"
        if (targetFramework.StartsWith("net") && targetFramework.Contains("."))
        {
            var versionPart = targetFramework.Substring(3); // Remove "net"
            var dotIndex = versionPart.IndexOf('.');
            if (dotIndex > 0)
            {
                var majorVersionStr = versionPart.Substring(0, dotIndex);
                if (int.TryParse(majorVersionStr, out int majorVersion))
                {
                    return majorVersion;
                }
            }
        }
        return null;
    }

    private bool IsFrameworkSpecificPackage(string packageId)
    {
        // List of package prefixes that typically follow .NET framework versioning
        var frameworkSpecificPrefixes = new[]
        {
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions",
            "Microsoft.Maui",
            "Microsoft.WindowsDesktop",
            "Microsoft.VisualStudio.Web.CodeGeneration"
        };

        // Also check for packages that have .NET-aligned versioning patterns
        var frameworkSpecificExactMatches = new[]
        {
            "System.Text.Json"
        };

        return frameworkSpecificPrefixes.Any(prefix => packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
               frameworkSpecificExactMatches.Any(exact => string.Equals(packageId, exact, StringComparison.OrdinalIgnoreCase));
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

                    // Use framework-aware checking based on applicable frameworks from conditions
                    if (package.ApplicableFrameworks.Any())
                    {
                        // For conditional packages, use only the frameworks they apply to

                        latestVersion = await GetLatestVersionForFrameworksAsync(package.Id, package.ApplicableFrameworks, includePrerelease, cts.Token);
                    }
                    else if (package.TargetFrameworks.Any())
                    {
                        // For non-conditional packages, use all target frameworks
                        latestVersion = await GetLatestVersionForFrameworksAsync(package.Id, package.TargetFrameworks, includePrerelease, cts.Token);
                    }
                    else
                    {
                        latestVersion = await GetLatestVersionAsync(package.Id, includePrerelease, cts.Token);
                    }

                    if (!string.IsNullOrEmpty(latestVersion))
                    {
                        package.LatestVersion = latestVersion;

                        // Get additional metadata (with timeout)
                        try
                        {
                            var packageInfo = await GetPackageInfoAsync(package.Id, includePrerelease, cts.Token);
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
}