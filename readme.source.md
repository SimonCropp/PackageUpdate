# <img src="https://raw.githubusercontent.com/SimonCropp/PackageUpdate/master/src/icon.png" height="40px"> MarkdownSnippets

A [dotnet tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) that updates packages all .csproj in a directory.


## Installation

Ensure [dotnet CLI is installed](https://docs.microsoft.com/en-us/dotnet/core/tools/).

**There is known a issue with dotnet tools on macOS and Linux that results in [installed tools not being discovered in the current path](https://github.com/dotnet/cli/issues/9321). The workaround is to add `~/.dotnet/tools` to the PATH.**

Install [PackageUpdate](https://nuget.org/packages/PackageUpdate/)

```ps
dotnet tool install -g PackageUpdate
```


## Usage

```ps
packageupdate C:\Code\TargetDirectory
```

If no directory is passed the current directory will be used.


### Behavior

 * Recursively scan the target directory for all `*.csproj` files.
 * Call [dotnet list package](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-list-package) to get the list of pending packages.
 * Call [dotnet add package](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-add-package) with the package and version.


## Release Notes

See [closed milestones](https://github.com/SimonCropp/PackageUpdate/milestones?state=closed).


## Icon

Icon courtesy of [The Noun Project](http://thenounproject.com) and is licensed under Creative Commons Attribution as:

> ["Down"](https://thenounproject.com/AlfredoCreates/collection/arrows-5-glyph/) by [Alfredo Creates](https://thenounproject.com/AlfredoCreates) from The Noun Project