# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PackageUpdate is a .NET global tool that updates NuGet packages for all solutions in a directory. It only supports solutions using Central Package Management (CPM) via `Directory.Packages.props`.

## Requirements

- .NET SDK 10 (specified in src/global.json)
- Uses C# preview language features (`LangVersion>preview`)
- Central Package Management (CPM) is required for all target solutions

## Build and Test Commands

```bash
# Build the solution
dotnet build src --configuration Release

# Run tests
dotnet test src --configuration Release --no-build --no-restore

# Build and test in one go (from src directory)
dotnet build src
dotnet test src --no-build

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

- **Updater.cs**: Core update logic
  - Parses `Directory.Packages.props` XML
  - Respects `Pinned="true"` attribute to skip packages
  - Queries NuGet sources for latest versions via NuGet.Protocol API
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
- xUnit v3
- Verify.XunitV3 for snapshot testing
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
- Tests with `dotnet test src --configuration Release`
