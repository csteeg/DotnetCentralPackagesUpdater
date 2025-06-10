using System.Xml.Linq;
using System.Text.RegularExpressions;
using CentralNuGetUpdater.Models;

namespace CentralNuGetUpdater.Services;

public class DirectoryPackagesParser
{
    public async Task<List<PackageInfo>> ParseDirectoryPackagesAsync(string filePath, SolutionInfo? solutionInfo = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Directory.Packages.props file not found at: {filePath}");
        }

        var packages = new List<PackageInfo>();

        try
        {
            var doc = await Task.Run(() => XDocument.Load(filePath));

            // Extract properties and variables from the file
            var properties = ExtractProperties(doc, solutionInfo);

            var packageVersionElements = doc.Descendants("PackageVersion");

            foreach (var element in packageVersionElements)
            {
                var include = element.Attribute("Include")?.Value;
                var version = element.Attribute("Version")?.Value;
                var condition = element.Attribute("Condition")?.Value;

                if (!string.IsNullOrEmpty(include) && !string.IsNullOrEmpty(version))
                {
                    // Resolve variables in version
                    var resolvedVersion = ResolveVariables(version, properties, solutionInfo);

                    var packageInfo = new PackageInfo
                    {
                        Id = include,
                        CurrentVersion = resolvedVersion,
                        OriginalVersionExpression = version != resolvedVersion ? version : null,
                        Condition = condition
                    };

                    // Determine applicable frameworks based on condition
                    if (!string.IsNullOrEmpty(condition) && solutionInfo != null)
                    {
                        packageInfo.ApplicableFrameworks = EvaluateConditionForFrameworks(condition, solutionInfo.AllTargetFrameworks).ToList();
                        packageInfo.TargetFrameworks = packageInfo.ApplicableFrameworks;
                    }
                    else if (solutionInfo != null)
                    {
                        // No condition means it applies to all frameworks
                        packageInfo.TargetFrameworks = solutionInfo.AllTargetFrameworks.ToList();
                        packageInfo.ApplicableFrameworks = solutionInfo.AllTargetFrameworks.ToList();
                    }

                    packages.Add(packageInfo);
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
                var condition = element.Attribute("Condition")?.Value;
                if (string.IsNullOrEmpty(include)) continue;

                // Find matching package based on ID and condition
                var packageToUpdate = packages.FirstOrDefault(p =>
                    p.Id == include &&
                    p.IsSelected &&
                    p.HasUpdate &&
                    p.Condition == condition);

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

    private Dictionary<string, string> ExtractProperties(XDocument doc, SolutionInfo? solutionInfo)
    {
        var properties = new Dictionary<string, string>();

        // Extract properties from PropertyGroup elements
        foreach (var propertyGroup in doc.Descendants("PropertyGroup"))
        {
            foreach (var property in propertyGroup.Elements())
            {
                if (!string.IsNullOrEmpty(property.Value))
                {
                    properties[property.Name.LocalName] = property.Value;
                }
            }
        }

        // Handle conditional properties (like the MsLibsVersion example)
        if (solutionInfo != null)
        {
            var frameworks = solutionInfo.AllTargetFrameworks;

            // Process Choose/When conditions
            foreach (var choose in doc.Descendants("Choose"))
            {
                foreach (var when in choose.Descendants("When"))
                {
                    var condition = when.Attribute("Condition")?.Value;
                    if (!string.IsNullOrEmpty(condition) && EvaluateCondition(condition, frameworks))
                    {
                        foreach (var propertyGroup in when.Descendants("PropertyGroup"))
                        {
                            foreach (var property in propertyGroup.Elements())
                            {
                                if (!string.IsNullOrEmpty(property.Value))
                                {
                                    properties[property.Name.LocalName] = property.Value;
                                }
                            }
                        }
                        break; // Take first matching condition
                    }
                }
            }
        }

        return properties;
    }

    private bool EvaluateCondition(string condition, HashSet<string> targetFrameworks)
    {
        // Simple condition evaluation for TargetFramework checks
        // Example: "'$(TargetFramework)' == 'net8.0'"
        var frameworkMatch = Regex.Match(condition, @"'\$\(TargetFramework\)'.*?==.*?'([^']+)'");
        if (frameworkMatch.Success)
        {
            var expectedFramework = frameworkMatch.Groups[1].Value;
            return targetFrameworks.Contains(expectedFramework);
        }

        // For now, default to true for Otherwise conditions
        return true;
    }

    private HashSet<string> EvaluateConditionForFrameworks(string condition, HashSet<string> availableFrameworks)
    {
        var applicableFrameworks = new HashSet<string>();

        // Handle equality conditions: '$(TargetFramework)' == 'net8.0'
        var equalityMatch = Regex.Match(condition, @"'\$\(TargetFramework\)'.*?==.*?'([^']+)'");
        if (equalityMatch.Success)
        {
            var expectedFramework = equalityMatch.Groups[1].Value;
            if (availableFrameworks.Contains(expectedFramework))
            {
                applicableFrameworks.Add(expectedFramework);
            }
            return applicableFrameworks;
        }

        // Handle inequality conditions: '$(TargetFramework)' != 'net8.0'
        var inequalityMatch = Regex.Match(condition, @"'\$\(TargetFramework\)'.*?!=.*?'([^']+)'");
        if (inequalityMatch.Success)
        {
            var excludedFramework = inequalityMatch.Groups[1].Value;
            foreach (var framework in availableFrameworks)
            {
                if (framework != excludedFramework)
                {
                    applicableFrameworks.Add(framework);
                }
            }
            return applicableFrameworks;
        }

        // For other conditions or unrecognized patterns, assume it applies to all frameworks
        return new HashSet<string>(availableFrameworks);
    }

    private string ResolveVariables(string version, Dictionary<string, string> properties, SolutionInfo? solutionInfo)
    {
        if (string.IsNullOrEmpty(version) || !version.Contains("$("))
        {
            return version;
        }

        var resolved = version;
        var variablePattern = @"\$\(([^)]+)\)";

        foreach (Match match in Regex.Matches(version, variablePattern))
        {
            var variableName = match.Groups[1].Value;
            if (properties.TryGetValue(variableName, out var variableValue))
            {
                resolved = resolved.Replace(match.Value, variableValue);
            }
        }

        return resolved;
    }
}