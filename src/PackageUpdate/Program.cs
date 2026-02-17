Logging.Init();
await CommandRunner.RunCommand(Inner, args);

static async Task Inner(string directory, string? package, bool build)
{
    Log.Information("TargetDirectory: {TargetDirectory}", directory);
    if (package != null)
    {
        Log.Information("Package: {Package}", package);
    }

    if (!Directory.Exists(directory))
    {
        Log.Information("Target directory does not exist: {TargetDirectory}", directory);
        Environment.Exit(1);
    }

    var totalStopwatch = Stopwatch.StartNew();
    using var cache = new SourceCacheContext
    {
        RefreshMemoryCache = true
    };
    foreach (var solution in FileSystem.FindSolutions(directory))
    {
        await TryProcessSolution(cache, solution, package, build);
    }

    if (build)
    {
        await DotnetStarter.Shutdown();
    }

    Log.Information("Completed in {Elapsed}", Formatter.FormatElapsed(totalStopwatch.Elapsed));
}

static async Task TryProcessSolution(SourceCacheContext cache, string solution, string? package, bool build)
{
    try
    {
        await ProcessSolution(cache, solution, package, build);
    }
    catch (Exception e)
    {
        Log.Error(
            """
            Failed to process solution: {Solution}.
            Error: {Message}
            """,
            solution,
            e.Message);
    }
}

static async Task ProcessSolution(SourceCacheContext cache, string solution, string? package, bool build)
{
    if (Excluder.ShouldExclude(solution))
    {
        Log.Information("  Exclude: {Solution}", solution);
        return;
    }

    Log.Information("  {Solution}", solution);

    var solutionDirectory = Directory.GetParent(solution)!.FullName;

    var props = Path.Combine(solutionDirectory, "Directory.Packages.props");
    if (!File.Exists(props))
    {
        Log.Error("    Only central packages supported. Skipping: {Solution}", solution);
        return;
    }

    var stopwatch = Stopwatch.StartNew();
    await Updater.Update(cache, props, package);
    Log.Information("    Updated in {Elapsed}", Formatter.FormatElapsed(stopwatch.Elapsed));

    if (build)
    {
        await DotnetStarter.Build(solution);
    }
}

