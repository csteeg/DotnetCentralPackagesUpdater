using Microsoft.Identity.Client;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace CentralNuGetUpdater.Services;

public class DeviceFlowAuthenticationService
{
    private readonly ConsoleUIService _uiService;
    private readonly Dictionary<string, string> _wellKnownTenants;

    public DeviceFlowAuthenticationService(ConsoleUIService uiService)
    {
        _uiService = uiService;
        _wellKnownTenants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "dev.azure.com", "https://login.microsoftonline.com/organizations" },
            { "pkgs.dev.azure.com", "https://login.microsoftonline.com/organizations" },
            { "azure.com", "https://login.microsoftonline.com/organizations" },
            { "visualstudio.com", "https://login.microsoftonline.com/organizations" },
            { "github.com", "https://github.com/login/device" },
            { "nuget.pkg.github.com", "https://github.com/login/device" }
        };
    }

    public async Task<NetworkCredential?> AuthenticateAsync(string sourceName, string sourceUrl)
    {
        try
        {
            if (IsAzureDevOpsSource(sourceUrl))
            {
                return await AuthenticateAzureDevOpsAsync(sourceName, sourceUrl);
            }
            else if (IsGitHubSource(sourceUrl))
            {
                _uiService.DisplayWarning("GitHub Packages requires a Personal Access Token. Please use the 'API Key' option instead.");
                return null;
            }
            else
            {
                _uiService.DisplayWarning($"Device flow authentication not supported for {sourceName}. Please use Username/Password or API Key.");
                return null;
            }
        }
        catch (Exception ex)
        {
            _uiService.DisplayError($"Device flow authentication failed: {ex.Message}");
            return null;
        }
    }

    private async Task<NetworkCredential?> AuthenticateAzureDevOpsAsync(string sourceName, string sourceUrl)
    {
        try
        {
            // Azure DevOps NuGet feeds require the Azure DevOps scope
            var app = PublicClientApplicationBuilder
                .Create("872cd9fa-d31f-45e0-9eab-6e460a02d1f1") // Visual Studio client ID
                .WithAuthority("https://login.microsoftonline.com/organizations")
                .WithRedirectUri("http://localhost")
                .Build();

            // Use the Azure DevOps resource ID instead of the problematic pkgs.dev.azure.com scope
            var scopes = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" };

            try
            {
                // Try silent authentication first
                var accounts = await app.GetAccountsAsync();
                if (accounts.Any())
                {
                    var result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                        .ExecuteAsync();

                    _uiService.DisplaySuccess("Using existing authentication");
                    return new NetworkCredential("", result.AccessToken);
                }
            }
            catch
            {
                // Silent auth failed, proceed with device flow
            }

            // Perform device flow authentication
            var deviceCodeResult = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeCallback =>
            {
                _uiService.DisplayDeviceCodeInstructions(deviceCodeCallback.UserCode, deviceCodeCallback.VerificationUrl);

                if (_uiService.PromptOpenBrowser())
                {
                    OpenBrowser(deviceCodeCallback.VerificationUrl);
                }

                _uiService.DisplayDeviceFlowProgress();
                return Task.FromResult(0);
            }).ExecuteAsync();

            _uiService.DisplayDeviceFlowSuccess();

            // Return credentials with empty username and access token as password
            return new NetworkCredential("", deviceCodeResult.AccessToken);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "authorization_pending")
        {
            _uiService.DisplayWarning("Authentication is still pending. Please complete the authentication in your browser.");
            return null;
        }
        catch (MsalClientException ex) when (ex.ErrorCode == "device_code_expired")
        {
            _uiService.DisplayError("Device code expired. Please try again.");
            return null;
        }
        catch (Exception ex)
        {
            _uiService.DisplayError($"Azure DevOps authentication failed: {ex.Message}");
            return null;
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch
        {
            // If we can't open the browser, user will have to do it manually
        }
    }

    private bool IsAzureDevOpsSource(string sourceUrl)
    {
        return sourceUrl.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
               sourceUrl.Contains("pkgs.dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
               sourceUrl.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsGitHubSource(string sourceUrl)
    {
        return sourceUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
               sourceUrl.Contains("nuget.pkg.github.com", StringComparison.OrdinalIgnoreCase);
    }

    public bool SupportsDeviceFlow(string sourceUrl)
    {
        return IsAzureDevOpsSource(sourceUrl);
    }

    public string GetProviderName(string sourceUrl)
    {
        if (IsAzureDevOpsSource(sourceUrl))
            return "Azure DevOps";
        if (IsGitHubSource(sourceUrl))
            return "GitHub";

        return "Unknown";
    }
}