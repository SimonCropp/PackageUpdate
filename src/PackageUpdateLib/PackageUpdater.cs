public static class PackageUpdater
{
    public static Task Run(string directory, string? package, bool build) =>
        Run(directory, package, build, null, null);

    public static Task Run(string directory, string? package, bool build, IProgress<UpdateProgress>? progress) =>
        Run(directory, package, build, progress, null);

    /// <summary>
    /// Updates the packages of every solution under the directory, then the tools of every
    /// dotnet-tools.json under it, reporting each file and each package as it starts on it.
    /// </summary>
    /// <param name="logger">Told what is updated, skipped, or fails. Nothing is logged when null.</param>
    public static async Task Run(string directory, string? package, bool build, IProgress<UpdateProgress>? progress, ILogger? logger)
    {
        logger ??= NullLogger.Instance;
        logger.LogInformation("TargetDirectory: {TargetDirectory}", directory);
        if (package != null)
        {
            logger.LogInformation("Package: {Package}", package);
        }

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Target directory does not exist: {directory}");
        }

        var totalStopwatch = Stopwatch.StartNew();
        using var cache = new SourceCacheContext
        {
            RefreshMemoryCache = true
        };
        // Multiple solutions can resolve to the same props file, so only update it once
        var updatedProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Found up front, so progress can say how many there are.
        var solutions = WithoutForks(directory, FileSystem.FindSolutions(directory), logger);

        // Tool manifests are scanned independently of solutions, since `.config/dotnet-tools.json`
        // usually sits at the repository root while the solution sits in a sub directory
        var manifests = WithoutForks(directory, FileSystem.FindToolManifests(directory), logger);

        var fileCount = solutions.Count + manifests.Count;
        var fileNumber = 0;
        foreach (var solution in solutions)
        {
            fileNumber++;
            var packageStarted = ReportStart(progress, solution, fileNumber, fileCount);
            await TryProcessSolution(cache, solution, package, build, directory, updatedProps, packageStarted, logger);
        }

        foreach (var manifest in manifests)
        {
            fileNumber++;
            var packageStarted = ReportStart(progress, manifest, fileNumber, fileCount);
            await TryProcessToolManifest(cache, manifest, package, packageStarted, logger);
        }

        if (build)
        {
            await DotnetStarter.Shutdown(logger);
        }

        logger.LogInformation("Completed in {Elapsed}", Formatter.FormatElapsed(totalStopwatch.Elapsed));
    }

    static List<string> WithoutForks(string directory, IEnumerable<string> files, ILogger logger)
    {
        var kept = new List<string>();
        foreach (var file in files)
        {
            if (ForkDetector.ShouldSkip(directory, file))
            {
                logger.LogInformation("  Skipping fork: {File}", file);
                continue;
            }

            kept.Add(file);
        }

        return kept;
    }

    // Reports the start of a file, and returns what reports each package in it.
    static Action<string, int, int>? ReportStart(IProgress<UpdateProgress>? progress, string file, int fileNumber, int fileCount)
    {
        if (progress == null)
        {
            return null;
        }

        progress.Report(new(file, fileNumber, fileCount, null, 0, 0));
        return (package, packageNumber, packageCount) =>
            progress.Report(new(file, fileNumber, fileCount, package, packageNumber, packageCount));
    }

    static async Task TryProcessSolution(SourceCacheContext cache, string solution, string? package, bool build, string targetDirectory, HashSet<string> updatedProps, Action<string, int, int>? packageStarted, ILogger logger)
    {
        try
        {
            await ProcessSolution(cache, solution, package, build, targetDirectory, updatedProps, packageStarted, logger);
        }
        catch (Exception e)
        {
            logger.LogError(
                """
                Failed to process solution: {Solution}.
                Error: {Message}
                """,
                solution,
                e.Message);
        }
    }

    static async Task TryProcessToolManifest(SourceCacheContext cache, string manifest, string? package, Action<string, int, int>? packageStarted, ILogger logger)
    {
        try
        {
            await ProcessToolManifest(cache, manifest, package, packageStarted, logger);
        }
        catch (Exception e)
        {
            logger.LogError(
                """
                Failed to process tool manifest: {Manifest}.
                Error: {Message}
                """,
                manifest,
                e.Message);
        }
    }

    static async Task ProcessToolManifest(SourceCacheContext cache, string manifest, string? package, Action<string, int, int>? packageStarted, ILogger logger)
    {
        if (Excluder.ShouldExclude(manifest))
        {
            logger.LogInformation("  Exclude: {Manifest}", manifest);
            return;
        }

        logger.LogInformation("  {Manifest}", manifest);

        var stopwatch = Stopwatch.StartNew();
        await ToolUpdater.Update(cache, manifest, package, packageStarted, logger);
        logger.LogInformation("    Updated in {Elapsed}", Formatter.FormatElapsed(stopwatch.Elapsed));
    }

    static async Task ProcessSolution(SourceCacheContext cache, string solution, string? package, bool build, string targetDirectory, HashSet<string> updatedProps, Action<string, int, int>? packageStarted, ILogger logger)
    {
        if (Excluder.ShouldExclude(solution))
        {
            logger.LogInformation("  Exclude: {Solution}", solution);
            return;
        }

        logger.LogInformation("  {Solution}", solution);

        var solutionDirectory = Directory.GetParent(solution)!.FullName;

        var propsLocation = FileSystem.FindPropsFile(solutionDirectory, targetDirectory);
        if (propsLocation is null)
        {
            logger.LogError("    Only central packages supported. Skipping: {Solution}", solution);
            return;
        }

        if (updatedProps.Add(propsLocation))
        {
            var stopwatch = Stopwatch.StartNew();
            await Updater.Update(cache, propsLocation, package, packageStarted, logger);
            logger.LogInformation("    Updated {Props} in {Elapsed}", propsLocation, Formatter.FormatElapsed(stopwatch.Elapsed));
        }
        else
        {
            logger.LogInformation("    Already updated: {Props}", propsLocation);
        }

        if (build)
        {
            await DotnetStarter.Build(solution, logger);
        }
    }
}
