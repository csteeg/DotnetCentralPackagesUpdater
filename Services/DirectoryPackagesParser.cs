using System.Xml.Linq;
using CentralNuGetUpdater.Models;

namespace CentralNuGetUpdater.Services;

public class DirectoryPackagesParser
{
    public async Task<List<PackageInfo>> ParseDirectoryPackagesAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Directory.Packages.props file not found at: {filePath}");
        }

        var packages = new List<PackageInfo>();

        try
        {
            var doc = await Task.Run(() => XDocument.Load(filePath));
            var packageVersionElements = doc.Descendants("PackageVersion");

            foreach (var element in packageVersionElements)
            {
                var include = element.Attribute("Include")?.Value;
                var version = element.Attribute("Version")?.Value;

                if (!string.IsNullOrEmpty(include) && !string.IsNullOrEmpty(version))
                {
                    packages.Add(new PackageInfo
                    {
                        Id = include,
                        CurrentVersion = version
                    });
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error parsing Directory.Packages.props: {ex.Message}", ex);
        }

        return packages;
    }

    public async Task UpdateDirectoryPackagesAsync(string filePath, List<PackageInfo> packages)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Directory.Packages.props file not found at: {filePath}");
        }

        try
        {
            var doc = await Task.Run(() => XDocument.Load(filePath));
            var packageVersionElements = doc.Descendants("PackageVersion");

            foreach (var element in packageVersionElements)
            {
                var include = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(include)) continue;

                var packageToUpdate = packages.FirstOrDefault(p => p.Id == include && p.IsSelected && p.HasUpdate);
                if (packageToUpdate != null)
                {
                    var versionAttribute = element.Attribute("Version");
                    if (versionAttribute != null)
                    {
                        versionAttribute.Value = packageToUpdate.LatestVersion!;
                    }
                }
            }

            await Task.Run(() => doc.Save(filePath));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error updating Directory.Packages.props: {ex.Message}", ex);
        }
    }
}