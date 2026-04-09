# PackageUpdate

A [dotnet tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) that updates NuGet packages for all solutions in a directory.

Only solutions using [Central Package Management (CPM)](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) are supported.


## Installation

```
dotnet tool install -g PackageUpdate
```


## Usage

```
packageupdate C:\Code\TargetDirectory
```

If no directory is passed the current directory will be used.


### Arguments

 * Target Directory: `packageupdate -t C:\Code\TargetDirectory`
 * Specific Package: `packageupdate --package PackageName`
 * Build After Update: `packageupdate --build`


## Features

 * Updates all packages across all solutions in a directory
 * Respects `Pinned="true"` attribute to skip specific packages
 * Automatically migrates deprecated packages to recommended alternatives
 * Preserves file formatting (newlines, indentation)
 * Queries all configured NuGet sources
 * Supports authenticated feeds


## Package Version Pinning

Add `Pinned="true"` to prevent a package from being updated:

```xml
<PackageVersion Include="System.ValueTuple" Version="4.5.0" Pinned="true" />
```


## Exclude Solutions

Set the `PackageUpdateIgnores` environment variable to skip specific directories:

```
setx PackageUpdateIgnores "AspNetCore,EntityFrameworkCore"
```
