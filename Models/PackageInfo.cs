namespace CentralNuGetUpdater.Models;

public class PackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string? LatestVersion { get; set; }
    public bool HasUpdate => !string.IsNullOrEmpty(LatestVersion) && CurrentVersion != LatestVersion;
    public bool IsSelected { get; set; }
    public string? Description { get; set; }
    public DateTime? Published { get; set; }
} 