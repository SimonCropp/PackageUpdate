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
2. **Fork and Worktree Detection** (`ForkDetector.ShouldSkip`, `WorktreeDetector.ShouldSkip`): Skips forked repos (those with an "upstream" remote) and linked git worktrees unless explicitly targeted
3. **Solution Validation**: Checks for `Directory.Packages.props` (CPM requirement) and applies exclusion rules
4. **Package Update** (`Updater.Update`): Updates package versions in `Directory.Packages.props`
5. **Optional Build** (`DotnetStarter.Build`): Builds solution after update if `--build` flag is provided
6. **Tool Manifest Update** (`ToolUpdater.Update`): Scans the target directory for `dotnet-tools.json` manifests and updates tool versions. This scan is independent of the solution scan, since `.config/dotnet-tools.json` usually sits at the repo root while the solution sits in a sub directory
7. **Exit Code** (Program.cs): 1 when the returned `UpdateResult.Succeeded` is false, otherwise 0

### Key Components

- **Updater.cs**: Core update and migration logic
  - Parses `Directory.Packages.props` XML
  - Respects `Pinned="true"` attribute to skip packages, on `PackageVersion` and on `PackageReference` with a `VersionOverride`. Matched case-insensitively (`Updater.IsPinned`), like MSBuild booleans
  - Queries NuGet sources for latest versions via NuGet.Protocol API
  - Detects deprecated packages and auto-migrates to alternatives when current version is deprecated
  - Preserves file formatting (newlines, indentation, trailing newlines)
  - Only considers stable versions when current version is stable
  - Only considers pre-release versions when current version is pre-release

- **ToolUpdater.cs**: Updates tool versions in a `dotnet-tools.json` manifest
  - Reuses `Updater.GetLatestVersion`, so tools follow the same stable/pre-release rules as packages
  - Respects `"pinned": true` on a tool entry, and the `--package` filter
  - Malformed manifests are logged as a warning and reported as a `ManifestUnreadable` problem, not thrown

- **UpdateResult.cs**, **PackageChange.cs**, **UpdateProblem.cs**: What `PackageUpdater.Run`, `Updater.Update` and `ToolUpdater.Update` return, so a caller such as Lode does not have to read the log to know the outcome
  - `Changes`: each version written, as a `Version`, `VersionOverride`, `Tool` or `Migration`. A migration is one change, with its `Alternative` and the csproj `Projects` whose references were renamed
  - `Problems`: each thing skipped or failed at. `IsFailure` (and so `Succeeded`) matches what is logged as an error: `Failed` (an exception, caught per solution or manifest) and `NotCentralPackages`. The rest are logged as warnings
  - A change is added only once its file is saved, so a file that throws part way leaves out what was not written. Keep each log call that reports a change or problem paired with adding it to the result
  - NuGet.Protocol's own logging (`NuGetLogger`) goes only to the logger, never into the result

- **ToolManifest.cs**: Parses a `dotnet-tools.json` manifest with `Utf8JsonReader`, capturing the byte range of each version value. Updates are spliced into the original bytes from the end of the file, so formatting is preserved exactly rather than being re-serialized

- **PackageSourceReader.cs**: Reads NuGet sources from NuGet.config hierarchy using NuGet settings infrastructure

- **ForkDetector.cs**: Detects forked git repos by checking for an "upstream" remote in `.git/config`. Skips forks found via recursive scanning; allows forks when explicitly targeted

- **WorktreeDetector.cs**: Detects linked git worktrees (a `.git` file whose `gitdir:` points into `.git/worktrees/<name>`, such as Claude Code's `.claude/worktrees`). Skips them when found via scanning, and allows them when explicitly targeted, using the same `FileSystem.IsTargeted` check as `ForkDetector`. Submodules (`.git` file pointing into `.git/modules`) are not skipped

- **Excluder.cs**: Solution filtering via `PackageUpdateIgnores` environment variable (comma-separated list)

- **FileSystem.cs**: Safe recursive directory traversal with UnauthorizedAccessException handling. Skips `node_modules` subdirectories (npm packages can ship native build solutions such as node-gyp's `binding.sln`) and `.git` subdirectories (never hold a solution, so walking them is wasted time), for both solutions and tool manifests. A target directory that is itself one of these is still scanned

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

### Dotnet Tools

Local dotnet tools are updated in every `dotnet-tools.json` manifest found under the target directory:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "8.0.0",
      "commands": [
        "dotnet-ef"
      ]
    },
    "packageupdate": {
      "version": "4.0.0",
      "commands": [
        "packageupdate"
      ],
      "pinned": true
    }
  }
}
```

Tools with `"pinned": true` are never updated. The dotnet CLI ignores the extra property, so `dotnet tool restore` and `dotnet tool list` continue to work.

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
- Queries all configured NuGet sources and selects highest version. Each source's candidates are newest first, so a source stops at the first candidate no newer than the best found so far, without fetching metadata for the rest
- Skips unlisted packages
- Respects the NuGet 8.0 [minimum package publish age](https://learn.microsoft.com/nuget/consume-packages/minimum-package-publish-age): the `minPublishAgeHours` attribute on a `packageSources` entry in nuget.config (set via `dotnet nuget add/update source --min-publish-age-hours`). Versions published more recently than that are skipped for that source
  - NuGet.Configuration reads the attribute onto `PackageSource.MinPublishAge` (zero when not set). A value that is not whole hours throws `NuGetConfigurationException` when settings load, which fails the solution or tool manifest as restore would. Do not parse the attribute by hand
  - `minPublishAgeExceptions` (read via `MinPublishAgeExceptions.GetMinPublishAgeExceptions`) exempts matching packages on every source. `PackageSourceReader.ReadConfigured` returns a `ConfiguredSources`, whose `For(package)` gives the sources to look a package up in: clones with no minimum age when the package is exempt. Internal callers look packages up via `For`, not the raw list
  - Under a nonzero minimum age, a version with no publish date is skipped (NuGet errors instead). See `Updater.CooldownSkipReason`
- Lookups are cached in `LookupCache` for the life of the `SourceCacheContext`, so each `PackageUpdater.Run` (which makes its own) sees newly published versions, and a long lived caller such as Lode does not get stale results. Within that, they are kept apart per set of sources (each source's url and `MinPublishAge`), since solutions under one directory can each have their own nuget.config. Never cache lookups in a static keyed only by package and version

### File Format Preservation

The Updater detects and preserves:
- Original newline style (CRLF, LF, CR)
- Trailing newline presence
- XML indentation (2 spaces)

## Solution Structure

```
src/
├── PackageUpdate/          # Tool project (dotnet global tool): thin CLI over PackageUpdateLib
│   ├── Program.cs          # Entry point, calls PackageUpdater.Run
│   ├── CommandRunner.cs    # CLI parsing
│   └── Logging.cs          # Serilog console/file setup, bridged to an ILogger for the lib
├── PackageUpdateLib/       # Library package with all update logic. Logs only via a passed in
│   │                       # Microsoft.Extensions.Logging ILogger (no Serilog reference)
│   ├── PackageUpdater.cs   # Public orchestration entry point (Run)
│   ├── UpdateResult.cs     # What Run returns: the changes written and the problems met
│   ├── UpdateProgress.cs   # What Run reports as it goes
│   ├── NuGetLogger.cs      # Forwards NuGet.Protocol logging to that ILogger
│   ├── Updater.cs          # Core update logic
│   ├── LookupCache.cs      # Version lookups, cached per SourceCacheContext and set of sources
│   ├── ToolUpdater.cs      # dotnet-tools.json update logic
│   ├── ToolManifest.cs     # dotnet-tools.json parsing and formatting preserving writes
│   ├── FileSystem.cs       # Solution and tool manifest discovery
│   ├── PackageSourceReader.cs  # NuGet source config
│   ├── ConfiguredSources.cs    # A directory's sources, and which packages skip their minimum age
│   └── ...
├── Tests/                  # Unit tests
├── Directory.Packages.props    # CPM configuration for this repo
└── PackageUpdate.slnx      # Solution file
```

## CI/CD

- Uses GitHub Actions for builds (`.github/workflows/build.yml`), publishing to NuGet on a tag via trusted publishing
- GitHub Actions for milestone releases and documentation
- Builds with `dotnet build src --configuration Release`
- Tests with `dotnet test --solution src/PackageUpdate.slnx --configuration Release`
