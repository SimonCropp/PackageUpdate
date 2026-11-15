/// <summary>
/// A version written by <see cref="PackageUpdater.Run(string, string?, bool, IProgress{UpdateProgress}?, ILogger?)"/>.
/// </summary>
/// <param name="Kind">What was changed.</param>
/// <param name="File">The Directory.Packages.props, csproj, or dotnet-tools.json written.</param>
/// <param name="Package">The package or tool. For a <see cref="PackageChangeKind.Migration"/>, the deprecated package that was replaced.</param>
/// <param name="From">The version before.</param>
/// <param name="To">The version after. For a <see cref="PackageChangeKind.Migration"/>, the version of <see cref="Alternative"/>.</param>
public record PackageChange(
    PackageChangeKind Kind,
    string File,
    string Package,
    NuGetVersion From,
    NuGetVersion To)
{
    /// <summary>For a <see cref="PackageChangeKind.Migration"/>, the package that replaced <see cref="Package"/>. Otherwise null.</summary>
    public string? Alternative { get; init; }

    /// <summary>
    /// For a <see cref="PackageChangeKind.Migration"/>, the csproj files whose PackageReference was
    /// renamed to <see cref="Alternative"/>. Otherwise empty.
    /// </summary>
    public IReadOnlyList<string> Projects { get; init; } = [];
}

public enum PackageChangeKind
{
    /// <summary>A PackageVersion in a Directory.Packages.props.</summary>
    Version,

    /// <summary>A VersionOverride on a PackageReference in a csproj.</summary>
    VersionOverride,

    /// <summary>A tool in a dotnet-tools.json.</summary>
    Tool,

    /// <summary>
    /// A deprecated PackageVersion in a Directory.Packages.props replaced by its alternative, along
    /// with the PackageReferences to it in the csproj files under it.
    /// </summary>
    Migration
}
