using System.Xml.Linq;
using System.Text.RegularExpressions;
using CentralNuGetUpdater.Models;

namespace CentralNuGetUpdater.Services;

public class DirectoryPackagesParser
{
    // Helper methods for case-insensitive XML parsing
    private static IEnumerable<XElement> GetDescendantsCaseInsensitive(XContainer container, string elementName)
    {
        return container.Descendants().Where(e => string.Equals(e.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase));
    }

    private static XAttribute? GetAttributeCaseInsensitive(XElement element, string attributeName)
    {
        return element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsElementNameMatch(XElement element, string elementName)
    {
        return string.Equals(element.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase);
    }

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

            // Parse both PackageVersion and GlobalPackageReference elements (case-insensitive)
            var packageElements = GetDescendantsCaseInsensitive(doc, "PackageVersion")
                .Concat(GetDescendantsCaseInsensitive(doc, "GlobalPackageReference"));

            foreach (var element in packageElements)
            {
                var include = GetAttributeCaseInsensitive(element, "Include")?.Value;
                var version = GetAttributeCaseInsensitive(element, "Version")?.Value;
                var condition = GetAttributeCaseInsensitive(element, "Condition")?.Value;
                var privateAssets = GetAttributeCaseInsensitive(element, "PrivateAssets")?.Value;
                var includeAssets = GetAttributeCaseInsensitive(element, "IncludeAssets")?.Value;
                var isGlobal = IsElementNameMatch(element, "GlobalPackageReference");
                
                // Check for exclusion attribute: FreezeVersion="true"
                var freezeVersionAttr = GetAttributeCaseInsensitive(element, "FreezeVersion")?.Value;
                var isExcluded = !string.IsNullOrEmpty(freezeVersionAttr) && freezeVersionAttr.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(include) && !string.IsNullOrEmpty(version))
                {
                    // Resolve variables in version
                    var resolvedVersion = ResolveVariables(version, properties, solutionInfo);

                    // Check for PrivateAssets in child elements if not found as attribute
                    if (string.IsNullOrEmpty(privateAssets))
                    {
                        var privateAssetsElement = GetDescendantsCaseInsensitive(element, "PrivateAssets").FirstOrDefault();
                        privateAssets = privateAssetsElement?.Value;
                    }

                    // Check for IncludeAssets in child elements if not found as attribute
                    if (string.IsNullOrEmpty(includeAssets))
                    {
                        var includeAssetsElement = GetDescendantsCaseInsensitive(element, "IncludeAssets").FirstOrDefault();
                        includeAssets = includeAssetsElement?.Value;
                    }

                    var packageInfo = new PackageInfo
                    {
                        Id = include,
                        CurrentVersion = resolvedVersion,
                        OriginalVersionExpression = version != resolvedVersion ? version : null,
                        Condition = condition,
                        IsGlobal = isGlobal,
                        PrivateAssets = privateAssets,
                        IncludeAssets = includeAssets,
                        HasPrivateAssets = !string.IsNullOrEmpty(privateAssets) &&
                                         privateAssets.Equals("All", StringComparison.OrdinalIgnoreCase),
                        IsAnalyzerPackage = IsAnalyzerOrTestPackage(include, privateAssets, includeAssets),
                        IsExcluded = isExcluded
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
            var packageElements = GetDescendantsCaseInsensitive(doc, "PackageVersion")
                .Concat(GetDescendantsCaseInsensitive(doc, "GlobalPackageReference"));

            foreach (var element in packageElements)
            {
                var include = GetAttributeCaseInsensitive(element, "Include")?.Value;
                var condition = GetAttributeCaseInsensitive(element, "Condition")?.Value;
                var isGlobal = IsElementNameMatch(element, "GlobalPackageReference");
                if (string.IsNullOrEmpty(include)) continue;

                // Find matching package based on ID, condition, and global status
                var packageToUpdate = packages.FirstOrDefault(p =>
                    p.Id == include &&
                    p.IsSelected &&
                    p.HasUpdate &&
                    !p.IsExcluded &&
                    p.Condition == condition &&
                    p.IsGlobal == isGlobal);

                if (packageToUpdate != null)
                {
                    var versionAttribute = GetAttributeCaseInsensitive(element, "Version");
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

        // Extract properties from PropertyGroup elements (case-insensitive)
        foreach (var propertyGroup in GetDescendantsCaseInsensitive(doc, "PropertyGroup"))
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

            // Process Choose/When conditions (case-insensitive)
            foreach (var choose in GetDescendantsCaseInsensitive(doc, "Choose"))
            {
                foreach (var when in GetDescendantsCaseInsensitive(choose, "When"))
                {
                    var condition = GetAttributeCaseInsensitive(when, "Condition")?.Value;
                    if (!string.IsNullOrEmpty(condition) && EvaluateCondition(condition, frameworks))
                    {
                        foreach (var propertyGroup in GetDescendantsCaseInsensitive(when, "PropertyGroup"))
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

    private bool IsAnalyzerOrTestPackage(string packageId, string? privateAssets, string? includeAssets)
    {
        // Check if package has PrivateAssets="All" (common for analyzers)
        var hasPrivateAssetsAll = !string.IsNullOrEmpty(privateAssets) &&
                                 privateAssets.Equals("All", StringComparison.OrdinalIgnoreCase);

        // Check if IncludeAssets contains "analyzers" 
        var includesAnalyzers = !string.IsNullOrEmpty(includeAssets) &&
                               includeAssets.Contains("analyzers", StringComparison.OrdinalIgnoreCase);

        // Check for common analyzer package name patterns
        var analyzerNamePatterns = new[]
        {
            "analyzer", "analyzers", "codeanalysis", "stylecop", "sonar", "roslynator",
            "meziantou", "idisposable", "asyncfixer", "codecracker", "refactoring",
            "diagnostic", "fxcop", "security", "reliability", "performance"
        };

        var hasAnalyzerNamePattern = analyzerNamePatterns.Any(pattern =>
            packageId.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        // Check for specific well-known analyzer packages
        var knownAnalyzerPackages = new[]
        {
            "Microsoft.CodeAnalysis.NetAnalyzers",
            "Microsoft.CodeAnalysis.Analyzers",
            "Microsoft.CodeAnalysis.CSharp.Analyzers",
            "Microsoft.CodeAnalysis.VisualBasic.Analyzers",
            "StyleCop.Analyzers",
            "SonarAnalyzer.CSharp",
            "Roslynator.Analyzers",
            "Meziantou.Analyzer",
            "IDisposableAnalyzers",
            "AsyncFixer",
            "Microsoft.CodeQuality.Analyzers",
            "Microsoft.NetCore.Analyzers",
            "Microsoft.NetFramework.Analyzers",
            "Text.Analyzers",
            "Microsoft.CodeAnalysis.BannedApiAnalyzers",
            "Microsoft.CodeAnalysis.PublicApiAnalyzers"
        };

        var isKnownAnalyzer = knownAnalyzerPackages.Any(known =>
            packageId.Equals(known, StringComparison.OrdinalIgnoreCase));

        // Check for test packages that often have framework compatibility issues
        var knownTestPackages = new[]
        {
            "xunit.v3",
            "xunit",
            "xunit.core",
            "xunit.extensibility.core",
            "xunit.runner.visualstudio",
            "coverlet.collector",
            "coverlet.msbuild",
            "Microsoft.NET.Test.Sdk",
            "NUnit",
            "NUnit3TestAdapter",
            "MSTest.TestAdapter",
            "MSTest.TestFramework"
        };

        var isKnownTestPackage = knownTestPackages.Any(known =>
            packageId.Equals(known, StringComparison.OrdinalIgnoreCase));

        // Check for test-related name patterns
        var testNamePatterns = new[]
        {
            "test", "testing", "coverlet", "coverage", "mock", "fake", "stub"
        };

        var hasTestNamePattern = testNamePatterns.Any(pattern =>
            packageId.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        return hasPrivateAssetsAll || includesAnalyzers || hasAnalyzerNamePattern ||
               isKnownAnalyzer || isKnownTestPackage || hasTestNamePattern;
    }
}