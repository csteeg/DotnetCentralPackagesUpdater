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
  -p, --path <path>        Path to Directory.Packages.props file or the directory containing it [default: current directory]
  -c, --config <config>    Path to nuget.config file (optional)
  --pre, --prerelease      Include prerelease versions when checking for updates [default: False]
  -d, --dry-run           Show what would be updated without making changes [default: False]
  --version               Show version information
  -?, -h, --help          Show help and usage information
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

#### Combine options

```bash
cpup --path "C:\MyProject" --prerelease --dry-run
```

## Central Package Management Setup

If your project doesn't use Central Package Management yet, you need to:

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

## NuGet.config Support

The tool automatically discovers and uses your `nuget.config` file. It supports:

- **Package Sources**: Respects enabled/disabled sources
- **Source Mapping**: Uses package source mapping rules
- **Authentication**: Works with authenticated feeds
- **Fallback Sources**: Uses fallback feeds when configured

Example `nuget.config`:

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
