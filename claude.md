# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PackageUpdate is a .NET global tool that updates NuGet packages for all solutions in a directory. It only supports solutions using Central Package Management (CPM) via `Directory.Packages.props`.

## Requirements

- .NET SDK 10 (specified in src/global.json)
- Uses C# preview language features (`LangVersion>preview`)
- Central Package Management (CPM) is required for all target solutions

## Coding Conventions

### Lambda Expressions

Always use underscore `_` for single-parameter lambda expressions instead of named parameters:

```csharp
// ✅ Correct
packages.Where(_ => _.Id == "MyPackage")
packages.FirstOrDefault(_ => _.Version == "1.0.0")
packages.OrderByDescending(_ => _)
elements.Any(_ => _.IsEnabled)

// ❌ Incorrect - don't use named parameters
packages.Where(p => p.Id == "MyPackage")
packages.FirstOrDefault(pkg => pkg.Version == "1.0.0")
elements.Any(e => e.IsEnabled)
```

This applies even when the parameter is used multiple times in the expression:

```csharp
// ✅ Correct
xml.Descendants("PackageVersion")
    .FirstOrDefault(_ =>
        string.Equals(
            _.Attribute("Include")?.Value,
            packageName,
            StringComparison.OrdinalIgnoreCase))

// ❌ Incorrect
xml.Descendants("PackageVersion")
    .FirstOrDefault(e =>
        string.Equals(
            e.Attribute("Include")?.Value,
            packageName,
            StringComparison.OrdinalIgnoreCase))
```

**Exception:** Use descriptive parameter names when creating complex anonymous types or when the lambda body is long and clarity would benefit from a meaningful name:

```csharp
// Named parameter acceptable for complex Select projections
var packageVersions = xml.Descendants("PackageVersion")
    .Select(element => new
    {
        Element = element,
        Package = element.Attribute("Include")?.Value,
        CurrentVersion = element.Attribute("Version")?.Value,
        Pinned = element.Attribute("Pinned")?.Value == "true"
    })
```

## Build and Test Commands

```bash
# Build the solution
dotnet build src --configuration Release

# Run tests
dotnet test --solution src/PackageUpdate.slnx --configuration Release --no-build --no-restore

# Build and test in one go (from src directory)
dotnet build src
dotnet test --solution src/PackageUpdate.slnx --no-build

# Install the tool locally for testing
dotnet pack src/PackageUpdate/PackageUpdate.csproj
dotnet tool install -g --add-source ./nupkgs PackageUpdate

# Run the tool
packageupdate <target-directory>
packageupdate --package <package-name>
packageupdate --build
```

## Architecture

### Core Workflow (Program.cs)

1. **Solution Discovery** (`FileSystem.FindSolutions`): Recursively scans target directory for `*.sln` and `*.slnx` files
2. **Solution Validation**: Checks for `Directory.Packages.props` (CPM requirement) and applies exclusion rules
3. **Package Update** (`Updater.Update`): Updates package versions in `Directory.Packages.props`
4. **Optional Build** (`DotnetStarter.Build`): Builds solution after update if `--build` flag is provided

### Key Components

- **Updater.cs**: Core update and migration logic
  - Parses `Directory.Packages.props` XML
  - Respects `Pinned="true"` attribute to skip packages
  - Queries NuGet sources for latest versions via NuGet.Protocol API
  - Detects deprecated packages and auto-migrates to alternatives when current version is deprecated
  - Preserves file formatting (newlines, indentation, trailing newlines)
  - Only considers stable versions when current version is stable
  - Only considers pre-release versions when current version is pre-release

- **PackageSourceReader.cs**: Reads NuGet sources from NuGet.config hierarchy using NuGet settings infrastructure

- **Excluder.cs**: Solution filtering via `PackageUpdateIgnores` environment variable (comma-separated list)

- **FileSystem.cs**: Safe recursive directory traversal with UnauthorizedAccessException handling

- **DotnetStarter.cs**: Process management for `dotnet build` commands with 60-second timeout

- **CommandRunner.cs**: CLI argument parsing using CommandLineParser library

### Testing

Tests use:
- TUnit
- Verify.TUnit for snapshot testing
- Located in `src/Tests/`

## Important Patterns

### Central Package Management

The tool only works with CPM. Each solution must have a `Directory.Packages.props` file with this structure:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="PackageName" Version="1.0.0" />
    <PackageVersion Include="PinnedPackage" Version="1.0.0" Pinned="true" />
  </ItemGroup>
</Project>
```

### Package Pinning

Packages with `Pinned="true"` attribute are never updated, even when explicitly targeted via `--package` flag.

### Package Migration

The tool automatically detects and migrates deprecated packages when an alternative is available.

#### How It Works

When updating packages, PackageUpdate checks if the **current version** of a package is marked as deprecated in NuGet. If the package has an alternative specified and that alternative is available in configured NuGet sources, the tool will automatically migrate:

1. Replaces the `Include` attribute with the alternative package name
2. Sets the `Version` to the latest version of the alternative (or the minimum version from the range if specified)
3. Logs the migration with deprecation reason

#### Migration Examples

```xml
<!-- Before -->
<PackageVersion Include="WindowsAzure.Storage" Version="9.3.3" />

<!-- After (migrated) -->
<PackageVersion Include="Azure.Storage.Common" Version="12.26.0" />
```

#### Migration Behavior

- **Pinned packages**: Never migrated (Pinned="true" is respected)
- **No alternative available**: Package version updated normally, warning logged
- **Alternative not found**: Package version updated normally, warning logged
- **Alternative already exists**: Migration skipped, warning logged, both packages remain
- **--package flag**: Migrations still occur for specified deprecated packages
- **Current version check**: Only migrates if the **current** version is deprecated, not if only newer versions are deprecated

#### Migration Logging

Successful migration:
```
Migrated WindowsAzure.Storage -> Azure.Storage.Common (Version: 12.26.0) [Deprecated: Legacy]
```

Deprecation warnings (when no migration possible):
```
Package WindowsAzure.Storage is deprecated but has no alternative. Reasons: Legacy
Package WindowsAzure.Storage is deprecated with alternative Azure.Storage.Common, but alternative already exists
```

### Version Selection Logic

- Uses `FindPackageByIdResource` to query all versions efficiently
- Filters to only consider versions greater than current
- Respects pre-release semantics (stable → stable, pre-release → any)
- Queries all configured NuGet sources and selects highest version
- Skips unlisted packages

### File Format Preservation

The Updater detects and preserves:
- Original newline style (CRLF, LF, CR)
- Trailing newline presence
- XML indentation (2 spaces)

## Solution Structure

```
src/
├── PackageUpdate/          # Main tool project (dotnet global tool)
│   ├── Program.cs          # Entry point and orchestration
│   ├── Updater.cs          # Core update logic
│   ├── CommandRunner.cs    # CLI parsing
│   ├── FileSystem.cs       # Solution discovery
│   ├── PackageSourceReader.cs  # NuGet source config
│   └── ...
├── Tests/                  # Unit tests
├── Directory.Packages.props    # CPM configuration for this repo
└── PackageUpdate.slnx      # Solution file
```

## CI/CD

- Uses AppVeyor for builds
- GitHub Actions for milestone releases and documentation
- Builds with `dotnet build src --configuration Release`
- Tests with `dotnet test --solution src/PackageUpdate.slnx --configuration Release`
