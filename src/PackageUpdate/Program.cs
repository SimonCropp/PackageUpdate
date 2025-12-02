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

    foreach (var solution in FileSystem.EnumerateFiles(directory, "*.sln"))
    {
        await TryProcessSolution(solution, package, build);
    }

    foreach (var solution in FileSystem.EnumerateFiles(directory, "*.slnx"))
    {
        await TryProcessSolution(solution, package, build);
    }

    await Shutdown();
}

static Task Shutdown()
{
    Log.Information("Shutdown dotnet build");
    return DotnetStarter.StartDotNet(
        arguments: "build-server shutdown",
        directory: Environment.CurrentDirectory,
        timeout: 20000);
}

static async Task TryProcessSolution(string solution, string? package, bool build)
{
    try
    {
        await ProcessSolution(solution, package, build);
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

static async Task ProcessSolution(string solution, string? package, bool build)
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
    await Updater.Update(props, package);

    if (build)
    {
        await Build(solution);
    }
}

static Task Build(string solution)
{
    Log.Information("    Build {Solution}", solution);
    return DotnetStarter.StartDotNet(
        arguments: $"build {solution} --no-restore --nologo",
        directory: Directory.GetParent(solution)!.FullName,
        timeout: 0);
}