using NuGet.Configuration;
using System.Net;

namespace CentralNuGetUpdater.Services;

public class NuGetAuthenticationService
{
    private readonly ISettings _settings;
    private readonly ConsoleUIService _uiService;
    private readonly DeviceFlowAuthenticationService _deviceFlowService;
    private readonly Dictionary<string, NetworkCredential> _sessionCredentials;
    private readonly HashSet<string> _skippedFeeds;

    public NuGetAuthenticationService(ISettings settings, ConsoleUIService uiService)
    {
        _settings = settings;
        _uiService = uiService;
        _deviceFlowService = new DeviceFlowAuthenticationService(uiService);
        _sessionCredentials = new Dictionary<string, NetworkCredential>();
        _skippedFeeds = new HashSet<string>();
    }

    public NetworkCredential? GetCredentialsForSource(string sourceName, string sourceUrl)
    {
        // Check if this feed was already skipped
        if (_skippedFeeds.Contains(sourceName))
        {
            return null;
        }

        // First, check if we have session credentials
        if (_sessionCredentials.TryGetValue(sourceName, out var sessionCred))
        {
            return sessionCred;
        }

        // Check if credentials exist in NuGet config
        var existingCreds = GetStoredCredentials(sourceName);
        if (existingCreds != null)
        {
            return existingCreds;
        }

        // If no credentials found, prompt user interactively
        return PromptForCredentials(sourceName, sourceUrl);
    }

    private NetworkCredential? GetStoredCredentials(string sourceName)
    {
        try
        {
            var packageSourceProvider = new PackageSourceProvider(_settings);
            var packageSource = packageSourceProvider.LoadPackageSources()
                .FirstOrDefault(s => s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

            if (packageSource?.Credentials != null)
            {
                return new NetworkCredential(packageSource.Credentials.Username, packageSource.Credentials.Password);
            }
        }
        catch
        {
            // Ignore errors when trying to retrieve stored credentials
        }

        return null;
    }

    private NetworkCredential? PromptForCredentials(string sourceName, string sourceUrl)
    {
        try
        {
            var authType = _uiService.PromptForAuthenticationType(sourceName);

            switch (authType)
            {
                case "Device Flow (Browser)":
                    if (_deviceFlowService.SupportsDeviceFlow(sourceUrl))
                    {
                        var deviceFlowResult = _deviceFlowService.AuthenticateAsync(sourceName, sourceUrl).GetAwaiter().GetResult();
                        if (deviceFlowResult != null)
                        {
                            // Store in session
                            _sessionCredentials[sourceName] = deviceFlowResult;

                            // Ask if user wants to save permanently (though tokens are usually short-lived)
                            if (_uiService.ConfirmSaveCredentials(sourceName))
                            {
                                SaveApiKeyToConfig(sourceName, deviceFlowResult.Password);
                            }
                        }
                        return deviceFlowResult;
                    }
                    else
                    {
                        _uiService.DisplayWarning($"Device flow not supported for {sourceName}. Please choose another authentication method.");
                        return PromptForCredentials(sourceName, sourceUrl); // Retry with different method
                    }

                case "Username/Password":
                    var (username, password) = _uiService.PromptForCredentials(sourceName);
                    var credential = new NetworkCredential(username, password);

                    // Store in session
                    _sessionCredentials[sourceName] = credential;

                    // Ask if user wants to save permanently
                    if (_uiService.ConfirmSaveCredentials(sourceName))
                    {
                        SaveCredentialsToConfig(sourceName, username, password);
                    }

                    return credential;

                case "API Key":
                    var apiKey = _uiService.PromptForApiKey(sourceName);
                    var apiCredential = new NetworkCredential("", apiKey);

                    // Store in session
                    _sessionCredentials[sourceName] = apiCredential;

                    // Ask if user wants to save permanently
                    if (_uiService.ConfirmSaveCredentials(sourceName))
                    {
                        SaveApiKeyToConfig(sourceName, apiKey);
                    }

                    return apiCredential;

                case "Skip this feed":
                    _uiService.DisplayWarning($"Skipping authentication for {sourceName}");
                    _skippedFeeds.Add(sourceName);
                    return null;

                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            _uiService.DisplayError($"Failed to get credentials for {sourceName}: {ex.Message}");
            return null;
        }
    }

    private void SaveCredentialsToConfig(string sourceName, string username, string password)
    {
        try
        {
            var packageSourceProvider = new PackageSourceProvider(_settings);
            var sources = packageSourceProvider.LoadPackageSources().ToList();
            var packageSource = sources.FirstOrDefault(s => s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

            if (packageSource != null)
            {
                // Create new source with credentials
                var newSource = new PackageSource(packageSource.Source, packageSource.Name, packageSource.IsEnabled)
                {
                    Credentials = new PackageSourceCredential(
                        sourceName,
                        username,
                        password,
                        true, // store password in clear text - NuGet will encrypt it
                        null)
                };

                // Replace the source in the list
                var index = sources.IndexOf(packageSource);
                sources[index] = newSource;

                packageSourceProvider.SavePackageSources(sources);
                _uiService.DisplaySuccess($"Credentials saved for {sourceName}");
            }
        }
        catch (Exception ex)
        {
            _uiService.DisplayError($"Failed to save credentials: {ex.Message}");
        }
    }

    private void SaveApiKeyToConfig(string sourceName, string apiKey)
    {
        try
        {
            var packageSourceProvider = new PackageSourceProvider(_settings);
            var sources = packageSourceProvider.LoadPackageSources().ToList();
            var packageSource = sources.FirstOrDefault(s => s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

            if (packageSource != null)
            {
                // Create new source with API key as password
                var newSource = new PackageSource(packageSource.Source, packageSource.Name, packageSource.IsEnabled)
                {
                    Credentials = new PackageSourceCredential(
                        sourceName,
                        "", // Empty username for API key
                        apiKey,
                        true,
                        null)
                };

                // Replace the source in the list
                var index = sources.IndexOf(packageSource);
                sources[index] = newSource;

                packageSourceProvider.SavePackageSources(sources);
                _uiService.DisplaySuccess($"API key saved for {sourceName}");
            }
        }
        catch (Exception ex)
        {
            _uiService.DisplayError($"Failed to save API key: {ex.Message}");
        }
    }

    public void ClearSessionCredentials()
    {
        _sessionCredentials.Clear();
        _skippedFeeds.Clear();
    }

    public bool IsAuthenticationError(Exception exception)
    {
        var message = exception.Message.ToLowerInvariant();
        var fullExceptionText = exception.ToString().ToLowerInvariant();

        // Check for explicit HTTP status codes
        if (message.Contains("401") || message.Contains("403") ||
            message.Contains("unauthorized") || message.Contains("forbidden"))
        {
            return true;
        }

        // Check for authentication-related keywords
        if (message.Contains("authentication") || message.Contains("credentials") ||
            message.Contains("sign in") || message.Contains("login"))
        {
            return true;
        }

        // Service index errors are often authentication related for private feeds
        if (message.Contains("unable to load the service index"))
        {
            // If it's any Azure DevOps feed, assume it's auth-related
            if (message.Contains("dev.azure.com") || message.Contains("pkgs.dev.azure.com") ||
                message.Contains("visualstudio.com") || fullExceptionText.Contains("dev.azure.com"))
            {
                return true;
            }
        }

        // Response status errors that might indicate authentication issues
        if (message.Contains("response status code does not indicate success"))
        {
            return true;
        }

        // Network or connection errors to Azure DevOps feeds
        if ((message.Contains("network") || message.Contains("connection") ||
             message.Contains("timeout") || message.Contains("ssl")) &&
            (message.Contains("dev.azure.com") || message.Contains("pkgs.dev.azure.com") ||
             fullExceptionText.Contains("dev.azure.com")))
        {
            return true;
        }

        // HTTP client errors that might indicate auth issues
        if (exception is HttpRequestException)
        {
            return message.Contains("401") || message.Contains("403") ||
                   message.Contains("unauthorized") || message.Contains("forbidden");
        }

        // Generic "failed to retrieve" errors for Azure DevOps
        if (message.Contains("failed to retrieve") &&
            (message.Contains("dev.azure.com") || fullExceptionText.Contains("dev.azure.com")))
        {
            return true;
        }

        return false;
    }

    public void LogAuthenticationError(Exception exception, string packageId, string sourceName)
    {
        _uiService.DisplayWarning($"Potential authentication error for {packageId} from {sourceName}:");
        _uiService.DisplayWarning($"Error: {exception.Message}");
        _uiService.DisplayWarning($"Exception type: {exception.GetType().Name}");
    }
}