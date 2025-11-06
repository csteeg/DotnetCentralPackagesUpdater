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

    // Prerelease properties
    public bool IsCurrentVersionPrerelease
    {
        get
        {
            try
            {
                var nugetVersion = NuGetVersion.Parse(CurrentVersion);
                return nugetVersion.IsPrerelease;
            }
            catch
            {
                // If parsing fails, check for common prerelease indicators
                var lowerVersion = CurrentVersion.ToLowerInvariant();
                return lowerVersion.Contains("alpha") ||
                       lowerVersion.Contains("beta") ||
                       lowerVersion.Contains("rc") ||
                       lowerVersion.Contains("preview") ||
                       lowerVersion.Contains("pre") ||
                       lowerVersion.Contains("-");
            }
        }
    }

    // Framework-aware properties
    public List<string> TargetFrameworks { get; set; } = new();
    public string? OriginalVersionExpression { get; set; } // For variables like $(MsLibsVersion)
    public Dictionary<string, string> FrameworkSpecificVersions { get; set; } = new(); // For future use

    // Conditional package properties
    public string? Condition { get; set; } // The original condition from the XML
    public List<string> ApplicableFrameworks { get; set; } = new(); // Which frameworks this package applies to
    public bool IsGlobal { get; set; } // Whether this is a GlobalPackageReference

    // Analyzer and build tool properties
    public bool HasPrivateAssets { get; set; } // Whether this package has PrivateAssets="All"
    public bool IsAnalyzerPackage { get; set; } // Whether this is likely an analyzer package
    public string? PrivateAssets { get; set; } // The actual PrivateAssets value
    public string? IncludeAssets { get; set; } // The IncludeAssets value

    // Exclusion properties
    public bool IsExcluded { get; set; } // Whether this package should be excluded from updates
}