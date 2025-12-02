Logging.Init();
await CommandRunner.RunCommand(Inner, args);

static async Task Inner(string directory, string? package, bool build)
{
    Log.Information("TargetDirectory: {TargetDirectory}", directory);
    Log.Information("Package: {Package}", package);
    if (!Directory.Exists(directory))
    {
        Log.Information("Target directory does not exist: {TargetDirectory}", directory);
        Environment.Exit(1);
    }

    using var cache = new SourceCacheContext();
    foreach (var solution in FileSystem.EnumerateFiles(directory, "*.sln"))
    {
        await TryProcessSolution(cache, solution, package, build);
    }

    foreach (var solution in FileSystem.EnumerateFiles(directory, "*.slnx"))
    {
        await TryProcessSolution(cache, solution, package, build);
    }

    if (build)
    {
        await DotnetStarter.Shutdown();
    }
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
            """, solution, e.Message);
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
        Log.Error("    Directory.Packages.props not found. Only central packages supported. Solution: {Solution}", solution);
        return;
    }

    Log.Information("    Found Directory.Packages.props. Processing only central packages");
    await Updater.Update(cache, props, package);

    if (build)
    {
        await DotnetStarter.Build(solution);
    }
}