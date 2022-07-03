# <img src="/src/icon.png" height="30px"> PackageUpdate

[![Build status](https://ci.appveyor.com/api/projects/status/sq3dnh0uyl7sf9uv/branch/main?svg=true)](https://ci.appveyor.com/project/SimonCropp/PackageUpdate)
[![NuGet Status](https://img.shields.io/nuget/v/PackageUpdate.svg)](https://www.nuget.org/packages/PackageUpdate/)

A [dotnet tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) that updates packages for all solutions in a directory.


## NuGet package

https://nuget.org/packages/PackageUpdate/


## Installation

Ensure [dotnet CLI is installed](https://docs.microsoft.com/en-us/dotnet/core/tools/).

Install [PackageUpdate](https://nuget.org/packages/PackageUpdate/)

```ps
dotnet tool install -g PackageUpdate
```


## Usage

```ps
packageupdate C:\Code\TargetDirectory
```

If no directory is passed the current directory will be used.


### Arguments


#### Target Directory

```ps
packageupdate C:\Code\TargetDirectory
```

```ps
packageupdate -t C:\Code\TargetDirectory
```

```ps
packageupdate --target-directory C:\Code\TargetDirectory
```


#### Package

The package name to update. If not specified, all packages will be updated.

```ps
packageupdate -p packageName
```

```ps
packageupdate --package packageName
```


### Behavior

 * Recursively scan the target directory for all directories containing a `.sln` file.
 * Perform a [dotnet restore](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-restore) on the directory.
 * Recursively scan the directory for `*.csproj` files.
 * Call [dotnet list package](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package) to get the list of pending packages.
 * Call [dotnet add package](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-add-package) with the package and version.


## PackageUpdateIgnores

When processing multiple directories, it is sometimes desirable to "always ignore" certain directories. This can be done by adding a `PackageUpdateIgnores` environment variable:

```
setx PackageUpdateIgnores "AspNetCore,EntityFrameworkCore"
```

The value is comma separated.


## Add to Windows Explorer

Use [context-menu.reg](/src/context-menu.reg) to add PackageUpdate to the Windows Explorer context menu.

snippet: context-menu.reg


## Authenticated feed

To use authenticatyed feed, add the [packageSourceCredentials](https://docs.microsoft.com/en-us/nuget/reference/nuget-config-file#packagesourcecredentials) to the global nuget config:

```xml
<packageSourceCredentials>
<feedName>
    <add key="Username" value="username" />
    <add key="Password" value="api key" />
</feedName>
</packageSourceCredentials>
```


## Icon

[Update](https://thenounproject.com/search/?q=update&i=2060555) by [Andy Miranda](https://thenounproject.com/andylontuan88) from [The Noun Project](https://thenounproject.com/).
