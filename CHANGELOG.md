# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.1]

### Added

- **📦 Package Source Mapping Support**: Full support for NuGet package source mapping as specified in `nuget.config` files
- **Intelligent Source Selection**: Packages are only checked against their designated sources based on mapping patterns
- **Performance Optimization**: Reduces unnecessary network requests by avoiding queries to irrelevant package sources
- **Enterprise Security**: Prevents supply chain attacks by ensuring packages only come from designated, trusted sources

### Features

- **Pattern Matching**: Supports both exact package names and wildcard patterns (`MyCompany.*`, `*`)
- **Precedence Rules**: Exact matches > prefix patterns > wildcard patterns for deterministic source selection
- **Multi-Source Support**: Handles environments with multiple private feeds and public repositories
- **XML Configuration**: Direct parsing of `packageSourceMapping` from `nuget.config` files

### Technical Implementation

- **Smart Source Filtering**: `GetRepositoriesForPackage()` method filters sources per package based on mappings
- **XML Parsing**: Robust `LoadPackageSourceMapping()` method using System.Xml.Linq
- **Pattern Recognition**: `IsPackageMatchingPattern()` handles exact matches and prefix wildcards
- **Debug Information**: Comprehensive logging showing which sources each package maps to

### Example Configuration

```xml
<packageSourceMapping>
  <packageSource key="nuget.org">
    <package pattern="*" />
  </packageSource>
  <packageSource key="company-feed">
    <package pattern="MyCompany.*" />
    <package pattern="Internal.Tools" />
  </packageSource>
</packageSourceMapping>
```

**Performance Impact:**

- Before: 3 sources × 5 packages = 15 potential network requests
- After: Each package only queries its designated source = 5 targeted requests

### Security Benefits

This feature helps prevent supply chain attacks by ensuring packages only come from designated, trusted sources, making it impossible for malicious packages to be fetched from unexpected sources.

## [1.6.0]

### Added

- **🎯 Major Version Constraints for Conditional Packages**: Conditional packages with `TargetFramework` conditions now automatically constrain to the appropriate major version series
- **Framework-Aligned Version Selection**: Packages conditioned on `'$(TargetFramework)' == 'net8.0'` stay within 8.x versions, preventing compatibility issues
- **Smart Condition Parsing**: Automatically extracts framework versions from conditions to determine major version constraints
- **Regex-Based Analysis**: Robust parsing of TargetFramework conditions with various formatting styles

### Features

- **Automatic Constraint Application**: No configuration needed - constraints are automatically applied to conditional packages
- **Selective Application**: Only applies to packages with `TargetFramework` equality conditions (`==`)
- **Framework Series Alignment**:
  - `net8.0` conditions → constrains to 8.x versions
  - `net6.0` conditions → constrains to 6.x versions
  - `net9.0` conditions → constrains to 9.x versions
- **Non-Conditional Package Preservation**: Regular packages and other condition types work exactly as before

### Technical Improvements

- **Enhanced GetLatestVersionForFrameworksAsync**: Added PackageInfo parameter and major version filtering logic
- **New ExtractMajorVersionConstraintFromCondition**: Parses conditions like `'$(TargetFramework)' == 'net8.0'` to extract major version
- **Version Filtering**: Compatible versions are filtered by major version before framework compatibility checks
- **Debug Logging**: Added comprehensive debug messages for constraint application and version skipping decisions

### Examples

**Before (v1.5.0):**

```xml
<PackageVersion Include="Microsoft.AspNetCore.Authorization" Condition="'$(TargetFramework)' == 'net8.0'" Version="8.0.15" />
```

Could suggest updating to 9.0.6 (potentially breaking)

**After (v1.6.0):**

```xml
<PackageVersion Include="Microsoft.AspNetCore.Authorization" Condition="'$(TargetFramework)' == 'net8.0'" Version="8.0.15" />
```

Will only suggest updates within 8.x series (e.g., 8.0.17) - safe and compatible

### Why This Matters

Prevents compatibility issues where packages like `Microsoft.AspNetCore.Authorization 9.0.x` might have .NET Standard 2.0 compatibility on paper but aren't truly compatible with .NET 8.0 applications in practice. This feature ensures framework-specific conditional packages stay within their intended major version series.

## [1.5.0]

### Added

- **🔧 Analyzer Package Support**: Automatic detection and handling of analyzer packages and test tools
- **Smart Framework Bypass**: Packages with `PrivateAssets="All"` automatically skip framework compatibility checks
- **Enhanced Package Detection**: Intelligent identification of analyzer packages based on:
  - `PrivateAssets="All"` attribute detection
  - `IncludeAssets` containing "analyzers"
  - Common analyzer package name patterns (StyleCop, SonarAnalyzer, Roslynator, etc.)
  - Well-known analyzer packages (Microsoft.CodeAnalysis.NetAnalyzers, etc.)
  - Test packages (xunit.v3, coverlet.collector, Microsoft.NET.Test.Sdk, etc.)
- **Framework Check Override**: New `--disable-framework-check` option to bypass framework compatibility for all packages
- **Visual Package Indicators**: Package names now show "(Analyzer/Test)", "(Global)", and condition indicators

### Fixed

- **Analyzer Package Updates**: Resolved issue where analyzer packages couldn't be checked for updates due to framework compatibility constraints
- **Test Tool Updates**: Fixed update checking for test packages like xunit.v3, coverlet.collector, and Microsoft.NET.Test.Sdk
- **Framework Compatibility**: Analyzer packages now use simple version checking instead of framework-aware checking

### Features

- **Automatic Detection**: No configuration needed - analyzer packages are automatically identified and handled appropriately
- **Command Line Option**: `--disable-framework-check` or `--no-framework-check` to disable framework checking for all packages
- **Enhanced UI**: Clear visual distinction between regular packages, analyzer packages, and global packages
- **Backward Compatibility**: All existing functionality preserved while adding new capabilities

### Technical Improvements

- **PackageInfo Model**: Added `HasPrivateAssets`, `IsAnalyzerPackage`, `PrivateAssets`, and `IncludeAssets` properties
- **DirectoryPackagesParser**: Enhanced to detect PrivateAssets and IncludeAssets attributes (both XML attributes and child elements)
- **NuGetPackageService**: Modified to skip framework checks for analyzer packages automatically
- **ConsoleUIService**: Updated to display package type indicators in the UI

### Examples

```bash
# Default behavior (automatic analyzer detection)
cpup --path "C:\Projects\MyProject" --dry-run

# Disable framework checking for all packages
cpup --path "C:\Projects\MyProject" --disable-framework-check --dry-run
```

## [1.4.5]

### Added

- **🎯 Smart Prerelease Handling**: Automatically includes prerelease versions when checking for updates if the current package version is already a prerelease
- **Visual Prerelease Indicators**: Current prerelease versions are marked with "(pre)" in the UI for easy identification
- **Enhanced Version Detection**: Uses NuGet's official version parsing to accurately detect prerelease packages

### Features

- **Automatic Behavior**: No need to use `--prerelease` flag for packages already using preview versions
- **Mixed Mode Support**: Some packages can be stable while others use prereleases, each handled appropriately
- **UI Improvements**: Clear visual indication of which packages are using prerelease versions
- **Example**: `System.CommandLine 2.0.0-beta4.22272.1 (pre)` automatically checks for newer betas and stable releases

### Technical Improvements

- **NuGet Version API**: Uses `NuGetVersion.IsPrerelease` for accurate prerelease detection
- **Fallback Detection**: Secondary pattern matching for edge cases where version parsing fails
- **Per-Package Logic**: Each package is evaluated individually for prerelease inclusion

## [1.4.4]

### Fixed

- **Migration Service**: Fixed XML processing issue where Version attributes weren't properly removed from PackageReference items during migration
- **Project File Updates**: Migration now correctly removes all Version attributes to prevent conflicts with Central Package Management

## [1.4.3]

### Added

- **🚀 Migration Tool**: New `migrate` command to convert solutions from regular PackageReference to Central Package Management
- **Automated Conversion**: Scans all project files and extracts PackageReference items to create Directory.Packages.props
- **Version Consolidation**: Automatically selects the highest version when multiple projects reference different versions of the same package
- **Safe Migration**: Dry-run mode to preview changes before applying them
- **Project File Updates**: Automatically removes Version attributes from PackageReference items in project files

### Features

- **Command**: `cpup migrate --solution . --dry-run` to preview migration
- **Command**: `cpup migrate --solution .` to perform actual migration
- **Smart Analysis**: Detects both .sln files and directory-based project discovery
- **Progress Reporting**: Shows detailed progress and migration summary
- **Error Handling**: Graceful handling of malformed project files

### Technical Improvements

- **Target Framework**: Updated from .NET 8.0 to .NET 9.0
- **Migration Service**: New `CentralPackageMigrationService` for handling conversions
- **XML Processing**: Case-insensitive XML parsing for robust project file handling

## [1.4.2]

### Fixed

- **Framework Compatibility Protection**: Fixed issue where packages that dropped support for older frameworks (like netstandard2.0/2.1) would still be suggested for update
- Removed unsafe fallback that would suggest incompatible package versions when framework-aware checking failed
- Enhanced netstandard2.x compatibility detection to prevent breaking updates for multi-targeting projects
- Example: FluentValidation now correctly suggests v11.x (compatible) instead of v12.x (dropped netstandard2.x support)

### Technical Improvements

- Removed fallback to `GetLatestVersionAsync()` in framework-aware version checking
- Added specific logic to detect packages that dropped support for netstandard2.0/2.1
- More conservative approach when package dependency metadata is unavailable
- Improved debug logging for compatibility decisions

### Breaking Change Prevention

- Protects multi-targeting projects (`net8.0;net9.0;netstandard2.0;netstandard2.1`) from incompatible package updates
- Ensures all suggested package versions are compatible with ALL target frameworks in the project

## [1.4.1] - 2025-06-10

### Fixed

- **Case-Insensitive XML Parsing**: All XML tag names and attribute names are now parsed case-insensitively
- Resolves issues where packages with lowercase attribute names (e.g., `version="9.0.4"`) were not detected
- Improves robustness when handling Directory.Packages.props files with mixed XML casing conventions
- Supports all case variations: `PackageVersion`, `packageversion`, `PACKAGEVERSION`, etc.

### Technical Details

- Enhanced `DirectoryPackagesParser.cs` with case-insensitive XML element and attribute lookup
- Enhanced `SolutionAnalyzerService.cs` with case-insensitive XML parsing for project files
- Added comprehensive helper methods for case-insensitive XML operations
- Maintains backward compatibility with existing functionality

## [1.4.0] - 2025-06-10

### Added

- **Directory.Build.props Support**: Automatically detects target frameworks from Directory.Build.props files
- Enhanced framework inheritance when project files don't explicitly define target frameworks
- Better analysis for complex project structures with shared properties

### Changed

- **Improved Package Version Selection**: Replaced name-based heuristics with actual package metadata analysis
- Framework-aware updates now based on real package compatibility, not naming conventions
- Uses NuGet's official compatibility APIs for more accurate framework checking

### Fixed

- Fixed issue where `Microsoft.Extensions.Logging.Abstractions` and similar packages wouldn't suggest latest versions for supported frameworks
- Removed artificial version restrictions based on package naming patterns
- Fixed target framework detection when projects rely on Directory.Build.props for framework definitions

### Technical Improvements

- Removed `IsFrameworkSpecificPackage()` method that used hardcoded package name patterns
- Simplified framework compatibility logic to always use actual package metadata
- Better error handling for projects without explicit target framework definitions

## [1.3.0] - Previous Release

### Features

- Conditional package support with framework-specific conditions
- GlobalPackageReference support for analyzers and tools
- Framework-aware version updates
- Interactive package selection
- Dry run mode
- Authentication support for private feeds

## [1.2.0] - Previous Release

### Features

- NuGet.config support and authentication
- Progress indicators and UI improvements
- Error handling improvements

## [1.1.0] - Previous Release

### Features

- Basic Central Package Management support
- Command line interface
- Package version checking

## [1.0.0] - Initial Release

### Features

- Initial release with basic package update functionality
