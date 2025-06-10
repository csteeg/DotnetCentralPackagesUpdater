using NuGet.Versioning;

namespace CentralNuGetUpdater.Models;

public class PackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string? LatestVersion { get; set; }
    public bool HasUpdate
    {
        get
        {
            if (string.IsNullOrEmpty(LatestVersion))
                return false;

            try
            {
                var currentNuGetVersion = NuGetVersion.Parse(CurrentVersion);
                var latestNuGetVersion = NuGetVersion.Parse(LatestVersion);

                // Only suggest update if latest version is actually newer than current
                return latestNuGetVersion > currentNuGetVersion;
            }
            catch
            {
                // Fallback to string comparison if version parsing fails
                return CurrentVersion != LatestVersion;
            }
        }
    }
    public bool IsSelected { get; set; }
    public string? Description { get; set; }
    public DateTime? Published { get; set; }

    // Framework-aware properties
    public List<string> TargetFrameworks { get; set; } = new();
    public string? OriginalVersionExpression { get; set; } // For variables like $(MsLibsVersion)
    public Dictionary<string, string> FrameworkSpecificVersions { get; set; } = new(); // For future use

    // Conditional package properties
    public string? Condition { get; set; } // The original condition from the XML
    public List<string> ApplicableFrameworks { get; set; } = new(); // Which frameworks this package applies to
    public bool IsGlobal { get; set; } // Whether this is a GlobalPackageReference
}