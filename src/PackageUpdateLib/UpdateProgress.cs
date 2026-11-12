/// <summary>
/// Where <see cref="PackageUpdater.Run(string, string?, bool, IProgress{UpdateProgress}?)"/> has got to:
/// the solution or tool manifest it is on, and the package in it being checked.
/// </summary>
/// <param name="File">The solution or dotnet-tools.json being worked on.</param>
/// <param name="FileNumber">Its position, from 1, among the solutions then the tool manifests.</param>
/// <param name="FileCount">How many solutions and tool manifests there are to work on.</param>
/// <param name="Package">The package or tool being checked. Null when work on the file has just started.</param>
/// <param name="PackageNumber">Its position, from 1, in the file. 0 when <paramref name="Package"/> is null.</param>
/// <param name="PackageCount">How many packages or tools in the file are checked. 0 when <paramref name="Package"/> is null.</param>
public record UpdateProgress(
    string File,
    int FileNumber,
    int FileCount,
    string? Package,
    int PackageNumber,
    int PackageCount);
