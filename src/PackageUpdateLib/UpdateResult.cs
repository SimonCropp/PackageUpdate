/// <summary>
/// What <see cref="PackageUpdater.Run(string, string?, bool, IProgress{UpdateProgress}?, ILogger?)"/> changed,
/// and what it skipped or failed at. Only changes written to disk are listed, so a file that
/// throws part way leaves out what it had not yet saved.
/// </summary>
public class UpdateResult
{
    List<PackageChange> changes = [];
    List<UpdateProblem> problems = [];

    /// <summary>Each version changed and package migrated, in the order they were written.</summary>
    public IReadOnlyList<PackageChange> Changes => changes;

    /// <summary>Each thing skipped, not done, or failed at, in the order they happened.</summary>
    public IReadOnlyList<UpdateProblem> Problems => problems;

    /// <summary>False when any of <see cref="Problems"/> is a failure. See <see cref="UpdateProblem.IsFailure"/>.</summary>
    public bool Succeeded => !problems.Any(_ => _.IsFailure);

    internal void Add(PackageChange change) =>
        changes.Add(change);

    internal void Add(UpdateProblem problem) =>
        problems.Add(problem);
}
