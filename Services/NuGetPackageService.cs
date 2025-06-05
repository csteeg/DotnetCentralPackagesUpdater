using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
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

    public async Task<string?> GetLatestVersionAsync(string packageId, bool includePrerelease = false)
    {
        foreach (var repository in _sourceRepositories)
        {
            try
            {
                var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
                var versions = await resource.GetAllVersionsAsync(packageId, new SourceCacheContext(), _logger, CancellationToken.None);

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

    public async Task<PackageInfo?> GetPackageInfoAsync(string packageId, bool includePrerelease = false)
    {
        foreach (var repository in _sourceRepositories)
        {
            try
            {
                var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
                var packages = await metadataResource.GetMetadataAsync(packageId, includePrerelease, false,
                    new SourceCacheContext(), _logger, CancellationToken.None);

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
        _uiService.DisplayInfo($"Checking for updates for {packages.Count} packages...");

        var tasks = packages.Select(async package =>
        {
            try
            {
                var latestVersion = await GetLatestVersionAsync(package.Id, includePrerelease);
                if (!string.IsNullOrEmpty(latestVersion))
                {
                    package.LatestVersion = latestVersion;

                    // Get additional metadata
                    var packageInfo = await GetPackageInfoAsync(package.Id, includePrerelease);
                    if (packageInfo != null)
                    {
                        package.Description = packageInfo.Description;
                        package.Published = packageInfo.Published;
                    }
                }
                else
                {
                    _uiService.DisplayDebug($"No version found for {package.Id}");
                }
            }
            catch (Exception ex)
            {
                _uiService.DisplayError($"Error checking updates for {package.Id}: {ex.Message}");
            }
        });

        await Task.WhenAll(tasks);

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