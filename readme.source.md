# <img src="https://raw.githubusercontent.com/SimonCropp/PackageUpdate/master/src/icon.png" height="40px"> PackageUpdate

A [dotnet tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) that updates packages for all solutions in a directory.


## Installation

Ensure [dotnet CLI is installed](https://docs.microsoft.com/en-us/dotnet/core/tools/).

Install [PackageUpdate](https://nuget.org/packages/PackageUpdate/)

```ps
dotnet tool install -g PackageUpdate
```

**There is known a issue with dotnet tools on macOS and Linux that results in [installed tools not being discovered in the current path](https://github.com/dotnet/cli/issues/9321). The workaround is to add `~/.dotnet/tools` to the PATH.**


## Usage

```ps
packageupdate C:\Code\TargetDirectory
```

If no directory is passed the current directory will be used.


### Behavior

 * Recursively scan the target directory for all directories containing a `.sln` file.
 * Perform a [dotnet restore](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-restore) on the directory.
 * Recursively scan the directory for `*.csproj` files.
 * Call [dotnet list package](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package) to get the list of pending packages.
 * Call [dotnet add package](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-add-package) with the package and version.


## Release Notes

See [closed milestones](https://github.com/SimonCropp/PackageUpdate/milestones?state=closed).


## Icon

["Update"](https://thenounproject.com/search/?q=update&i=2060555) by [Andy Miranda](https://thenounproject.com/andylontuan88) from The Noun Project