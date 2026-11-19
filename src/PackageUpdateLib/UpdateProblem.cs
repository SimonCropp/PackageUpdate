/// <summary>
/// Something <see cref="PackageUpdater.Run(string, string?, bool, IProgress{UpdateProgress}?, ILogger?)"/>
/// skipped, could not do, or failed at.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="File">The solution, Directory.Packages.props, or dotnet-tools.json being worked on.</param>
/// <param name="Message">What happened, for a person. Names the package, when there is one, but not the file.</param>
public record UpdateProblem(
    UpdateProblemKind Kind,
    string File,
    string Message)
{
    /// <summary>The package or tool concerned, when there is one.</summary>
    public string? Package { get; init; }

    /// <summary>What was thrown, for a <see cref="UpdateProblemKind.Failed"/>. Otherwise null.</summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// True for a <see cref="UpdateProblemKind.Failed"/> or <see cref="UpdateProblemKind.NotCentralPackages"/>,
    /// the problems logged as errors: work on that solution or tool manifest stopped short. The others
    /// are logged as warnings, and the rest of the file was still updated.
    /// </summary>
    public bool IsFailure => Kind is UpdateProblemKind.Failed or UpdateProblemKind.NotCentralPackages;
}

public enum UpdateProblemKind
{
    /// <summary>
    /// Working on a solution or tool manifest threw, such as for a source that could not be reached,
    /// a file that could not be parsed, or a build that failed. What was already written stays written.
    /// </summary>
    Failed,

    /// <summary>A solution with no Directory.Packages.props, so it was skipped.</summary>
    NotCentralPackages,

    /// <summary>The package asked for is not in a Directory.Packages.props.</summary>
    PackageNotFound,

    /// <summary>A dotnet-tools.json that could not be parsed, so it was skipped.</summary>
    ManifestUnreadable,

    /// <summary>A deprecated package with no alternative, so its version was updated instead of it being migrated.</summary>
    DeprecatedWithoutAlternative,

    /// <summary>
    /// A deprecated package whose alternative is already in the Directory.Packages.props, so its version
    /// was updated instead of it being migrated.
    /// </summary>
    AlternativeAlreadyPresent,

    /// <summary>
    /// A deprecated package whose alternative is in none of the sources, so its version was updated
    /// instead of it being migrated.
    /// </summary>
    AlternativeNotFound,

    /// <summary>
    /// A package or tool that the nuget.config's packageSourceMapping maps to none of the sources, so it
    /// was not updated. Restore cannot find it either.
    /// </summary>
    NoMappedSource
}
