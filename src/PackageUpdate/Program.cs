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
        if (ForkDetector.ShouldSkip(directory, solution))
        {
            Log.Information("  Skipping fork: {Solution}", solution);
            continue;
        }

        await TryProcessSolution(cache, solution, package, build);
    }

    // Tool manifests are scanned independently of solutions, since `.config/dotnet-tools.json`
    // usually sits at the repository root while the solution sits in a sub directory
    foreach (var manifest in FileSystem.FindToolManifests(directory))
    {
        if (ForkDetector.ShouldSkip(directory, manifest))
        {
            Log.Information("  Skipping fork: {Manifest}", manifest);
            continue;
        }

        await TryProcessToolManifest(cache, manifest, package);
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

static async Task TryProcessToolManifest(SourceCacheContext cache, string manifest, string? package)
{
    try
    {
        await ProcessToolManifest(cache, manifest, package);
    }
    catch (Exception e)
    {
        Log.Error(
            """
            Failed to process tool manifest: {Manifest}.
            Error: {Message}
            """,
            manifest,
            e.Message);
    }
}

static async Task ProcessToolManifest(SourceCacheContext cache, string manifest, string? package)
{
    if (Excluder.ShouldExclude(manifest))
    {
        Log.Information("  Exclude: {Manifest}", manifest);
        return;
    }

    Log.Information("  {Manifest}", manifest);

    var stopwatch = Stopwatch.StartNew();
    await ToolUpdater.Update(cache, manifest, package);
    Log.Information("    Updated in {Elapsed}", Formatter.FormatElapsed(stopwatch.Elapsed));
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

