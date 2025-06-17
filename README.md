# Central NuGet Package Updater

A console application that helps you manage and update NuGet packages in .NET projects using Central Package Management (Directory.Packages.props). The tool respects your nuget.config source rules and provides an interactive interface to select which packages to update.

## Features

- ✅ **Central Package Management Support**: Works with `Directory.Packages.props` files
- ✅ **NuGet.config Compliance**: Respects package sources and configuration from `nuget.config`
- ✅ **Seamless Authentication**: Uses the same credential providers as `dotnet restore`
- ✅ **Interactive Selection**: Choose which packages to update with a beautiful console UI
- ✅ **Update Preview**: See current vs. latest versions before updating
- ✅ **Dry Run Mode**: Preview what would be updated without making changes
- ✅ **Prerelease Support**: Optionally include prerelease versions
- ✅ **Conditional Package Support**: Handles framework-specific conditional packages
- ✅ **GlobalPackageReference Support**: Manages global analyzer and tool packages
- ✅ **🔧 Analyzer Package Support**: Automatic detection and smart handling of analyzer packages and test tools
- ✅ **Framework-Aware Updates**: Intelligent compatibility checking based on actual package metadata
- ✅ **Directory.Build.props Support**: Automatically detects target frameworks from Directory.Build.props files
- ✅ **Detailed Information**: Shows package descriptions and publish dates
- ✅ **Error Handling**: Graceful handling of network issues and missing packages

## Prerequisites

- .NET 8.0 or later
- An existing .NET project using Central Package Management

## Installation

### Option 1: Global .NET Tool (Recommended)

Install as a global .NET tool for easy access from anywhere:

```bash
dotnet tool install -g CentralNuGetUpdater
```

Then use it anywhere:

```bash
cpup --help
```

### Option 2: Build from Source

1. Clone or download this repository
2. Navigate to the project directory
3. Build the application:

   ```bash
   dotnet build -c Release
   ```

4. Run the application:

   ```bash
   dotnet run -- [options]
   ```

### Option 3: Publish as Self-Contained

1. Publish the application for your platform:

   ```bash
   # Windows
   dotnet publish -c Release -r win-x64 --self-contained
   
   # macOS
   dotnet publish -c Release -r osx-x64 --self-contained
   
   # Linux
   dotnet publish -c Release -r linux-x64 --self-contained
   ```

2. The executable will be in `bin/Release/net8.0/[runtime]/publish/`

## Usage

### Basic Usage

Navigate to your .NET solution directory (containing `Directory.Packages.props`) and run:

```bash
# If installed as global tool
cpup

# If running from source
dotnet run
```

### Command Line Options

```bash
Central NuGet Package Updater - Check and update packages in Directory.Packages.props

Usage:
  cpup [options]

Options:
  -p, --path <path>                                Path to Directory.Packages.props file or the directory containing it [default: current directory]
  -c, --config <config>                            Path to nuget.config file (optional)
  --pre, --prerelease                              Include prerelease versions when checking for updates [default: False]
  -d, --dry-run                                    Show what would be updated without making changes [default: False]
  --disable-framework-check, --no-framework-check  Disable framework-aware checking (useful for analyzer packages) [default: False]
  --version                                        Show version information
  -?, -h, --help                                   Show help and usage information
```

### Examples

#### Check for updates in current directory

```bash
# Global tool
cpup

# From source
dotnet run
```

#### Check specific directory or file

```bash
# Using directory path
cpup --path "C:\MyProject"

# Using direct file path
cpup --path "C:\MyProject\Directory.Packages.props"

# With custom nuget.config
cpup --path "C:\MyProject" --config "C:\MyProject\nuget.config"
```

#### Include prerelease versions

```bash
cpup --prerelease
```

#### Dry run (preview only)

```bash
cpup --dry-run
```

#### Disable framework checking for analyzer packages

```bash
# Automatic analyzer detection (default behavior)
cpup --dry-run

# Disable framework checking for all packages
cpup --disable-framework-check --dry-run
```

#### Combine options

```bash
cpup --path "C:\MyProject" --prerelease --dry-run
```

## 🚀 Migration to Central Package Management

**NEW in v1.4.3!** The tool now includes an automated migration feature to convert existing solutions from regular PackageReference to Central Package Management.

### Quick Migration

```bash
# Preview migration changes (recommended first step)
cpup migrate --solution . --dry-run

# Perform the actual migration
cpup migrate --solution .

# Migrate specific solution file
cpup migrate --solution MySolution.sln --dry-run
```

### What the Migration Does

1. **📊 Analyzes** all project files in your solution
2. **📦 Extracts** all PackageReference items with versions
3. **🎯 Consolidates** packages (picks highest version when conflicts exist)
4. **📄 Creates** Directory.Packages.props with centralized versions
5. **🔧 Updates** project files by removing Version attributes from PackageReference items
6. **✅ Validates** the solution still builds correctly

### Migration Example

**Before Migration:**
```xml
<!-- ProjectA.csproj -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
<PackageReference Include="Serilog" Version="3.0.1" />

<!-- ProjectB.csproj -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="AutoMapper" Version="12.0.0" />
```

**After Migration:**
```xml
<!-- Directory.Packages.props (created) -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="AutoMapper" Version="12.0.0" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageVersion Include="Serilog" Version="3.0.1" />
  </ItemGroup>
</Project>

<!-- ProjectA.csproj (updated) -->
<PackageReference Include="Newtonsoft.Json" />
<PackageReference Include="Serilog" />

<!-- ProjectB.csproj (updated) -->
<PackageReference Include="Newtonsoft.Json" />
<PackageReference Include="AutoMapper" />
```

### Migration Features

- **🔍 Smart Discovery**: Works with both .sln files and directory-based project discovery
- **📈 Version Consolidation**: Automatically picks the highest version when projects have different versions
- **🛡️ Safe Migration**: Dry-run mode lets you preview all changes before applying
- **🎯 Progress Tracking**: Shows detailed progress and summary of changes
- **⚠️ Error Handling**: Gracefully handles malformed or problematic project files

### Migration Output Example

```
🔄 Starting migration to Central Package Management...
✓ Found 3 projects to analyze
  📦 ProjectA.csproj: 5 package references
  📦 ProjectB.csproj: 3 package references
  📦 Tests.csproj: 2 package references
✓ Found 8 unique packages across all projects

📋 Migration Preview:
  📄 Directory.Packages.props: C:\MyProject\Directory.Packages.props
  📦 Packages to centralize: 8
  🔧 Project files to modify: 3

📦 Packages that will be centralized:
  • AutoMapper → 12.0.0
  • Microsoft.Extensions.Logging → 8.0.1
  • Newtonsoft.Json → 13.0.3 (consolidated from versions: 13.0.1, 13.0.3)
  • Serilog → 3.0.1

🔧 Project files that will be modified:
  • ProjectA.csproj (5 packages)
  • ProjectB.csproj (3 packages)
  • Tests.csproj (2 packages)

💡 To perform the actual migration, run the command again without --dry-run
```

## Central Package Management Setup (Manual)

If you prefer to set up Central Package Management manually, you need to:

1. Create a `Directory.Packages.props` file in your solution root:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <!-- Add your packages here -->
  </ItemGroup>
</Project>
```

2. Update your project files (.csproj) to remove version attributes:

```xml
<!-- Before -->
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />

<!-- After -->
<PackageReference Include="Microsoft.Extensions.Logging" />
```

## 🔧 Analyzer Package Support

**NEW in v1.5.0!** The tool now provides intelligent handling of analyzer packages and test tools that often have framework compatibility issues.

### Automatic Detection

The tool automatically detects analyzer packages and test tools based on:

- **PrivateAssets="All"**: Packages with this attribute are automatically identified as analyzer packages
- **IncludeAssets containing "analyzers"**: Packages that explicitly include analyzer assets
- **Common Name Patterns**: Packages with names containing "analyzer", "stylecop", "sonar", "roslynator", etc.
- **Well-Known Packages**: Comprehensive list of known analyzer packages like:
  - `Microsoft.CodeAnalysis.NetAnalyzers`
  - `StyleCop.Analyzers`
  - `SonarAnalyzer.CSharp`
  - `Roslynator.Analyzers`
  - `xunit.v3`, `coverlet.collector`
  - And many more...

### Smart Framework Bypass

Analyzer packages automatically skip framework compatibility checks because:
- They typically work across all .NET versions
- Framework constraints often prevent legitimate updates
- They're build-time tools, not runtime dependencies

### Visual Indicators

Analyzer packages are clearly marked in the UI:

```
Packages with available updates:
┌─────────────────────────────┬─────────────────┬────────────────┐
│ Package                     │ Current Version │ Latest Version │
├─────────────────────────────┼─────────────────┼────────────────┤
│ Microsoft.NET.Test.Sdk      │ 17.13.0         │ 17.14.1        │
│ (Analyzer/Test)             │                 │                │
│ Roslynator.Analyzers        │ 4.12.7          │ 4.13.1         │
│ (Global, Analyzer/Test)     │                 │                │
│ SonarAnalyzer.CSharp        │ 9.32.0.97167    │ 10.11.0.117924 │
│ (Global, Analyzer/Test)     │                 │                │
└─────────────────────────────┴─────────────────┴────────────────┘
```

### Framework Check Override

For complete control, you can disable framework checking for all packages:

```bash
# Disable framework checking for all packages
cpup --disable-framework-check --dry-run

# Alternative alias
cpup --no-framework-check --dry-run
```

### Before vs. After

**Before v1.5.0:**
```
Packages with errors (couldn't check for updates):
• xunit.v3 (2.0.3)
• coverlet.collector (6.0.4)
• Microsoft.CodeAnalysis.NetAnalyzers (9.0.0)
• Roslynator.Analyzers (4.12.7)
• SonarAnalyzer.CSharp (9.32.0.97167)
```

**After v1.5.0:**
```
Packages with available updates:
• Microsoft.NET.Test.Sdk (Analyzer/Test): 17.13.0 → 17.14.1
• Roslynator.Analyzers (Global, Analyzer/Test): 4.12.7 → 4.13.1
• SonarAnalyzer.CSharp (Global, Analyzer/Test): 9.32.0.97167 → 10.11.0.117924
```

## GlobalPackageReference Support

The tool fully supports **GlobalPackageReference** items, which are used for packages that should be applied to all projects in a solution (typically analyzers, code style tools, and build tools).

### Global Package Management

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Global packages applied to all projects -->
    <GlobalPackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" PrivateAssets="All" />
    <GlobalPackageReference Include="StyleCop.Analyzers" Version="1.1.118" PrivateAssets="All" />
    <GlobalPackageReference Include="SonarAnalyzer.CSharp" Version="9.0.0" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <!-- Regular package versions -->
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### Global Package Features

- **Automatic Detection**: Global packages are automatically identified and labeled as "(Global)" in the UI
- **Update Management**: Global packages can be updated just like regular packages
- **Mixed Support**: Works seamlessly alongside regular PackageVersion items
- **Analyzer Focus**: Perfect for managing code analyzers, style checkers, and build tools

### Example Output with Global Packages

```
Packages with available updates:
┌──────────────────────────────────────────────┬─────────────────┬────────────────┐
│ Package                                      │ Current Version │ Latest Version │
├──────────────────────────────────────────────┼─────────────────┼────────────────┤
│ Microsoft.Extensions.Logging                 │ 8.0.0           │ 8.0.1          │
│ Microsoft.CodeAnalysis.NetAnalyzers (Global) │ 8.0.0           │ 9.0.0          │
│ SonarAnalyzer.CSharp (Global)                │ 9.0.0           │ 10.11.0        │
└──────────────────────────────────────────────┴─────────────────┴────────────────┘
```

## Directory.Build.props Support

The tool automatically detects target frameworks from `Directory.Build.props` files, enabling proper framework-aware package management even when individual project files don't explicitly define target frameworks.

### How It Works

1. **Automatic Discovery**: Walks up the directory tree to find `Directory.Build.props` files
2. **Property Inheritance**: Loads global properties like `TargetFramework` and `TargetFrameworks`
3. **Project Override**: Individual project files can still override global settings
4. **Framework Detection**: Uses detected frameworks for compatibility checking and version suggestions

### Example Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

When projects don't define their own `TargetFramework`, they inherit `net8.0;net9.0` from the `Directory.Build.props`, and the tool will use this for framework-aware package analysis.

## Conditional Package Support

The tool supports **conditional packages** with framework-specific conditions, which is especially useful for multi-targeting projects.

### Framework-Specific Packages

For projects that target multiple frameworks (e.g., `net8.0` and `net9.0`), you can use conditional packages:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Different versions for different frameworks -->
    <PackageVersion Include="Microsoft.AspNetCore.Authorization" Condition="'$(TargetFramework)' == 'net8.0'" Version="8.0.15" />
    <PackageVersion Include="Microsoft.AspNetCore.Authorization" Condition="'$(TargetFramework)' != 'net8.0'" Version="9.0.4" />
    
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Condition="'$(TargetFramework)' == 'net8.0'" Version="8.0.16" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Condition="'$(TargetFramework)' != 'net8.0'" Version="9.0.5" />
    
    <!-- Non-conditional packages work as usual -->
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### Framework-Aware Version Selection

The tool provides **intelligent framework-aware updates** based on actual package metadata:

- **Compatibility Analysis**: Checks each package version against its actual supported frameworks (not naming conventions)
- **Latest Compatible Versions**: Suggests the truly latest version that supports your target frameworks
- **Cross-Framework Support**: A package supporting both .NET 8.0 and .NET 9.0 will suggest the latest version for both frameworks
- **Example**: `Microsoft.Extensions.Logging.Abstractions 9.0.5` is correctly suggested for .NET 8.0 projects because it actually supports .NET 8.0

This approach ensures you get the **latest compatible version** rather than artificially restricting versions based on naming patterns.

### Example Output with Conditional Packages

```
Packages with available updates:
┌───────────────────────────────────────────────┬─────────────────┬────────────────┬───────────────────┐
│ Package                                       │ Current Version │ Latest Version │ Target Frameworks │
├───────────────────────────────────────────────┼─────────────────┼────────────────┼───────────────────┤
│ Microsoft.AspNetCore.Authorization            │ 8.0.15          │ 8.0.16         │ net8.0            │
│ ('$(TargetFramework)' == 'net8.0')            │                 │                │                   │
│ Microsoft.AspNetCore.Authorization            │ 9.0.4           │ 9.0.5          │ net9.0            │
│ ('$(TargetFramework)' != 'net8.0')            │                 │                │                   │
└───────────────────────────────────────────────┴─────────────────┴────────────────┴───────────────────┘
```

### Supported Condition Patterns

The tool supports the most common condition patterns:

- **Equality**: `Condition="'$(TargetFramework)' == 'net8.0'"`
- **Inequality**: `Condition="'$(TargetFramework)' != 'net8.0'"`
- **More complex conditions**: Parsed and evaluated appropriately

### Why Use Conditional Packages?

1. **Multi-targeting projects**: Use the optimal package version for each target framework
2. **Migration scenarios**: Gradually migrate from .NET 8 to .NET 9 while maintaining compatibility
3. **Framework-specific features**: Use framework-specific APIs and optimizations
4. **Dependency compatibility**: Ensure packages are compatible with specific .NET versions

## Framework Compatibility Protection

The tool includes **intelligent framework compatibility protection** that prevents incompatible package updates for multi-targeting projects.

### 🛡️ Automatic Compatibility Checking

When your project targets multiple frameworks (e.g., `net8.0;net9.0;netstandard2.0;netstandard2.1`), the tool:

- ✅ **Analyzes package metadata** to determine supported frameworks
- ✅ **Filters out incompatible versions** that dropped support for your target frameworks
- ✅ **Suggests the latest compatible version** instead of the absolute latest
- ✅ **Prevents breaking changes** from packages that dropped older framework support

### 📋 Example: FluentValidation Protection

```xml
<!-- Your project targets multiple frameworks -->
<PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;netstandard2.0;netstandard2.1</TargetFrameworks>
</PropertyGroup>
```

**Before (v1.4.1):** Tool would suggest FluentValidation 12.0.0 ❌ (breaks netstandard2.x)  
**After (v1.4.2):** Tool suggests FluentValidation 11.11.0 ✅ (compatible with all frameworks)

### 🎯 How It Works

1. **Framework Analysis**: Detects all target frameworks in your projects
2. **Metadata Inspection**: Downloads package dependency information from NuGet
3. **Compatibility Matrix**: Uses NuGet's official compatibility APIs
4. **Smart Filtering**: Only suggests versions compatible with ALL your target frameworks
5. **Conservative Approach**: When in doubt, skips the update rather than breaking compatibility

### 🚨 Protected Scenarios

The tool protects against common breaking changes:

- **FluentValidation 12.x**: Dropped netstandard2.0/2.1 support
- **System.Text.Json 8.x+**: Limited netstandard2.0 support  
- **Microsoft.Extensions.* 9.x**: Some packages dropped older framework support
- **Entity Framework 8.x+**: Changed minimum framework requirements

### ⚙️ Debug Information

Use the `--dry-run` flag to see compatibility decisions:

```bash
cpup --dry-run --path .
```

The tool will show debug information about why certain package versions were skipped for compatibility reasons.

## NuGet.config Support

The tool automatically discovers and uses your `nuget.config` file. It supports:

- **Package Sources**: Respects enabled/disabled sources
- **Package Source Mapping**: Uses package source mapping rules for secure and efficient package resolution
- **Authentication**: Works with authenticated feeds
- **Fallback Sources**: Uses fallback feeds when configured

### 📦 Package Source Mapping (v1.7.0+)

Package Source Mapping provides enterprise-grade security and performance by ensuring packages only come from designated sources. This prevents supply chain attacks and reduces unnecessary network requests.

#### Example `nuget.config` with Source Mapping:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="CompanyFeed" value="https://pkgs.dev.azure.com/company/_packaging/internal/nuget/v3/index.json" />
    <add key="HealthOSS" value="https://microsofthealthoss.pkgs.visualstudio.com/FhirServer/_packaging/Public/nuget/v3/index.json" />
  </packageSources>
  
  <packageSourceMapping>
    <!-- Microsoft Health packages only from Health OSS feed -->
    <packageSource key="HealthOSS">
      <package pattern="Microsoft.Health.*" />
    </packageSource>
    
    <!-- Company packages only from internal feed -->
    <packageSource key="CompanyFeed">
      <package pattern="MyCompany.*" />
      <package pattern="Internal.Tools" />
    </packageSource>
    
    <!-- Everything else from nuget.org -->
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

#### 🚀 Performance Benefits

**Without Source Mapping:**
- `MyCompany.Core` → Checks 3 sources = 3 network requests
- `Newtonsoft.Json` → Checks 3 sources = 3 network requests  
- `Microsoft.Health.Fhir` → Checks 3 sources = 3 network requests
- **Total: 9 requests for 3 packages**

**With Source Mapping:**
- `MyCompany.Core` → Only checks `CompanyFeed` = 1 request
- `Newtonsoft.Json` → Only checks `nuget.org` = 1 request
- `Microsoft.Health.Fhir` → Only checks `HealthOSS` = 1 request
- **Total: 3 requests for 3 packages (3x faster!)**

#### 🔒 Security Benefits

- **Prevents dependency confusion attacks** by ensuring packages only come from trusted sources
- **Eliminates package hijacking risks** from unauthorized feeds
- **Provides audit trail** of which source each package came from
- **Enforces organizational policies** about package sources

#### Pattern Matching Rules

1. **Exact Match**: `Microsoft.Extensions.Logging` matches only that specific package
2. **Prefix Wildcard**: `MyCompany.*` matches `MyCompany.Core`, `MyCompany.Authentication`, etc.
3. **Global Wildcard**: `*` matches any package (lowest precedence)

**Precedence Order:** Exact match > Longest prefix > Shorter prefix > Global wildcard

#### Tool Output with Source Mapping

```
✓ Credential service configured - will use same authentication as 'dotnet restore'
ℹ Package source mapping detected - packages will be checked against appropriate sources only
ℹ Loaded 3 package source(s)
ℹ   - nuget.org: https://api.nuget.org/v3/index.json
ℹ   - CompanyFeed: https://pkgs.dev.azure.com/company/_packaging/internal/nuget/v3/index.json
ℹ   - HealthOSS: https://microsofthealthoss.pkgs.visualstudio.com/FhirServer/_packaging/Public/nuget/v3/index.json

Packages with available updates:
┌────────────────────────────┬─────────────────┬────────────────┬────────────┬──────────────┐
│ Package                    │ Current Version │ Latest Version │ Published  │ Source       │
├────────────────────────────┼─────────────────┼────────────────┼────────────┼──────────────┤
│ Newtonsoft.Json            │ 12.0.1          │ 13.0.3         │ 2023-03-08 │ nuget.org    │
│ MyCompany.Core             │ 1.2.0           │ 1.3.1          │ 2024-01-15 │ CompanyFeed  │
│ Microsoft.Health.Fhir.Core │ 3.1.0           │ 4.0.445        │ 2024-02-07 │ HealthOSS    │
└────────────────────────────┴─────────────────┴────────────────┴────────────┴──────────────┘
```

### Basic `nuget.config` (No Source Mapping)

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="MyCompanyFeed" value="https://pkgs.dev.azure.com/company/_packaging/feed/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

## Authentication Support

The tool uses the **same credential providers as `dotnet restore`**, ensuring seamless authentication with private NuGet feeds without any additional setup.

### 🔐 How Authentication Works

1. **Automatic Integration**: The tool integrates with NuGet's built-in credential provider system
2. **Zero Configuration**: If `dotnet restore` works with your private feeds, this tool will work too
3. **Same Credentials**: Uses existing Azure Artifacts Credential Provider, AWS CodeArtifact, MyGet, and other installed credential providers
4. **Silent Authentication**: No interactive prompts - authentication happens transparently

### ✅ Supported Authentication Methods

The tool automatically supports any authentication method that `dotnet restore` supports:

- **Azure DevOps Artifacts** - Uses Azure Artifacts Credential Provider
- **GitHub Packages** - Uses stored PATs or GitHub CLI authentication  
- **AWS CodeArtifact** - Uses AWS credential providers
- **MyGet** - Uses stored API keys or credential providers
- **Corporate NuGet feeds** - Uses domain authentication or configured credentials
- **Any credential provider** - Supports NuGet's extensible credential provider system

### 🚀 Quick Setup for Azure DevOps

If you're using Azure DevOps and haven't set up authentication yet:

1. **Install Azure Artifacts Credential Provider:**

   ```bash
   # Using dotnet tool (recommended)
   dotnet tool install -g Azure.Artifacts.CredentialProvider
   
   # Or download from: https://github.com/microsoft/artifacts-credprovider
   ```

2. **Test with dotnet restore:**

   ```bash
   dotnet restore --interactive
   ```

3. **Run this tool - it will work automatically!**

### 🎯 Authentication Flow

```
✓ Credential service configured - will use same authentication as 'dotnet restore'
ℹ Loaded 2 package source(s)
ℹ   - nuget.org: https://api.nuget.org/v3/index.json
ℹ   - MyCompanyFeed: https://pkgs.dev.azure.com/company/_packaging/feed/nuget/v3/index.json
ℹ Checking for updates for 25 packages...
```

**That's it!** No interactive prompts, no manual credential entry - just seamless authentication using your existing setup.

### 🔒 Security & Credentials

- **Existing Credentials**: Uses credentials already configured for `dotnet restore`
- **No Storage**: The tool doesn't store or manage credentials directly
- **Secure**: Leverages NuGet's secure credential provider infrastructure
- **Consistent**: Same security model as all other .NET CLI tools

## How It Works

1. **Discovery**: Finds `Directory.Packages.props` in the specified directory
2. **Parsing**: Extracts all `PackageVersion` elements
3. **Configuration**: Loads NuGet sources from `nuget.config`
4. **Authentication**: Automatically uses the same credential providers as `dotnet restore`
5. **Version Check**: Queries each configured source for the latest version
6. **Interactive Selection**: Presents packages with updates for user selection (all selected by default)
7. **Update**: Modifies `Directory.Packages.props` with selected updates

## Output Example

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
                         Central NuGet Package Updater
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✓ Found 8 packages

Packages with available updates:
┌─────────────────────────────────────────┬─────────────────┬───────────────┬────────────┐
│ Package                                 │ Current Version │ Latest Version│ Published  │
├─────────────────────────────────────────┼─────────────────┼───────────────┼────────────┤
│ Microsoft.Extensions.DependencyInjection│ 7.0.0          │ 8.0.0         │ 2023-11-14 │
│ Newtonsoft.Json                         │ 13.0.1         │ 13.0.3        │ 2023-03-17 │
│ Serilog                                 │ 3.0.1          │ 3.1.1         │ 2023-10-16 │
└─────────────────────────────────────────┴─────────────────┴───────────────┴────────────┘

Select packages to update:
? Which packages would you like to update? 
  [X] Microsoft.Extensions.DependencyInjection (7.0.0 → 8.0.0)  ← All selected by default
  [X] Newtonsoft.Json (13.0.1 → 13.0.3)
  [X] Serilog (3.0.1 → 3.1.1)
```

## Publishing as Global Tool

To publish updates to NuGet.org:

1. **Update version** in `CentralNuGetUpdater.csproj`
2. **Create package:**

   ```bash
   dotnet pack -c Release
   ```

3. **Publish to NuGet.org:**

   ```bash
   dotnet nuget push bin/Release/CentralNuGetUpdater.*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
   ```

## Recent Improvements (v1.4.5)

### 🎯 NEW: Smart Prerelease Handling
- **Automatic Detection**: If a package is already using a prerelease version, automatically checks for newer prereleases and stable releases
- **Visual Indicators**: Prerelease packages are clearly marked with "(pre)" in the UI
- **Mixed Mode Support**: Some packages can be stable while others use prereleases - each handled appropriately
- **No Extra Flags**: No need to use `--prerelease` flag for packages already using preview versions

### Example: Prerelease Package Handling

```bash
# Your Directory.Packages.props contains:
# <PackageVersion Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />

cpup --path .
# Tool automatically checks for both newer betas AND stable releases for System.CommandLine
# Shows: System.CommandLine: 2.0.0-beta4.22272.1 (pre) → 2.0.0-beta5.23456.1
```

## Previous Improvements (v1.4.3)

### 🚀 Migration Tool
- **Automated Migration**: New `migrate` command converts solutions from regular PackageReference to Central Package Management
- **Smart Analysis**: Scans all projects and extracts package references automatically
- **Version Consolidation**: Picks highest version when multiple projects use different versions of the same package
- **Safe Preview**: Dry-run mode shows exactly what will be changed before applying
- **Complete Automation**: Creates Directory.Packages.props and updates all project files

### 🔧 Technical Improvements
- **Target Framework**: Updated from .NET 8.0 to .NET 9.0
- **Migration Service**: New comprehensive service for handling package reference conversions
- **Enhanced Command Line**: Added subcommand support for better user experience

## Previous Improvements (v1.4.2)

### 🛡️ Framework Compatibility Protection
- **Intelligent Compatibility Checking**: Prevents incompatible package updates for multi-targeting projects
- **Breaking Change Prevention**: Protects against packages that dropped support for older frameworks (netstandard2.0/2.1)
- **Enhanced Framework Analysis**: Uses NuGet's official compatibility APIs instead of unsafe fallbacks
- **Example Protection**: FluentValidation now correctly suggests v11.x (compatible) instead of v12.x (breaks netstandard2.x)
- **Conservative Approach**: When package metadata is unclear, skips update rather than risking compatibility

### 🔧 Technical Improvements
- **Removed Unsafe Fallback**: No longer falls back to absolute latest version when framework checking fails
- **Better Debug Logging**: Improved visibility into compatibility decisions
- **Enhanced NetStandard Detection**: Specific logic to protect netstandard2.0/2.1 targets

## Previous Improvements (v1.4.1)

### 🛡️ Enhanced XML Parsing Robustness
- **Case-Insensitive XML Parsing**: All XML tag names and attribute names are now parsed case-insensitively
- **Improved Package Detection**: Resolves issues where packages weren't detected due to XML case variations (e.g., `version="9.0.4"` vs `Version="9.0.4"`)
- **Mixed Casing Support**: Handles all XML case combinations (`PackageVersion`, `packageversion`, `PACKAGEVERSION`, etc.)
- **Backward Compatible**: Maintains full compatibility with existing functionality

## Previous Improvements (v1.4.0)

### 🎯 Enhanced Framework Detection
- **Directory.Build.props Support**: Automatically detects target frameworks from `Directory.Build.props` files
- **Better Inheritance**: Properly handles framework inheritance when project files don't explicitly define target frameworks
- **Improved Analysis**: More accurate framework detection for complex project structures

### 🚀 Smarter Package Version Selection
- **Metadata-Based Compatibility**: Replaced name-based heuristics with actual package metadata analysis
- **Latest Compatible Versions**: Now suggests the truly latest version that supports your target frameworks
- **Cross-Framework Packages**: Correctly handles packages that support multiple .NET versions
- **Example Fix**: `Microsoft.Extensions.Logging.Abstractions 9.0.5` is now correctly suggested for .NET 8.0 projects

### 🔧 Technical Improvements
- **Removed Artificial Restrictions**: No longer artificially restricts package versions based on naming patterns
- **Better Compatibility Checking**: Uses NuGet's official compatibility APIs
- **More Accurate Updates**: Framework-aware updates based on actual package support, not version number alignment

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Troubleshooting

### Common Issues

1. **"Directory.Packages.props not found"**
   - Ensure you're running the command in the correct directory
   - Use the `--path` option to specify the correct path

2. **"No packages found"**
   - Check that your `Directory.Packages.props` contains `PackageVersion` elements
   - Verify the XML format is correct

3. **"Failed to check version for package"**
   - Check your internet connection
   - Verify your `nuget.config` sources are accessible
   - Some corporate networks may require proxy configuration

4. **Authentication issues with private feeds**
   - Ensure `dotnet restore` works with your private feeds first
   - The tool uses the same credential providers and configuration as `dotnet restore`
   - Install the appropriate credential provider (e.g., Azure Artifacts Credential Provider for Azure DevOps)

5. **Corporate proxy issues**
   - Ensure your corporate proxy settings are configured in your NuGet configuration
   - Some corporate environments may require additional certificate configuration

### Getting Help

If you encounter issues:

1. Check the troubleshooting section above
2. Run with `--dry-run` to see what would happen without making changes
3. Verify your `nuget.config` is properly configured
4. For authentication issues, ensure `dotnet restore` works with your feeds first
5. Open an issue with details about your setup and the error message
