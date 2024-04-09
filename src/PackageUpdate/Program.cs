await CommandRunner.RunCommand(Inner, args);

static async Task Inner(string targetDirectory, string? package, bool build)
{
    Console.WriteLine($"TargetDirectory: {targetDirectory}");
    Console.WriteLine($"Package: {package}");
    if (!Directory.Exists(targetDirectory))
    {
        Console.WriteLine($"Target directory does not exist: {targetDirectory}");
        Environment.Exit(1);
    }

    foreach (var solution in FileSystem.EnumerateFiles(targetDirectory, "*.sln"))
    {
        await TryProcessSolution(solution, package, build);
    }

    await Shutdown();
}


static Task Shutdown()
{
    Console.WriteLine("Shutdown dotnet build");
    return DotnetStarter.StartDotNet(
        arguments: "build build-server shutdown",
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
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(
            $"""
             Failed to process solution: {solution}.
             Error: {e.Message}
             """);
        Console.ResetColor();
    }
}

static async Task ProcessSolution(string solution, string? package, bool build)
{
    if (Excluder.ShouldExclude(solution))
    {
        Console.WriteLine($"  Exclude: {solution}");
        return;
    }

    Console.WriteLine($"  {solution}");
    await SolutionRestore.Run(solution);

    var solutionDirectory = Directory.GetParent(solution)!.FullName;
    foreach (var project in ProjectFiles(solutionDirectory))
    {
        Console.WriteLine($"    {project.Replace(solutionDirectory, "").Trim(Path.DirectorySeparatorChar)}");
        foreach (var pending in await PendingUpdateReader.ReadPendingUpdates(project))
        {
            if (package == null)
            {
                await Update(project, pending.Package, pending.Latest);
                continue;
            }

            if (string.Equals(package, pending.Package, StringComparison.OrdinalIgnoreCase))
            {
                await Update(project, pending.Package, pending.Latest);
            }
        }
    }

    if (build)
    {
        await Build(solution);
    }
}

static async Task Update(string project, string package, string version)
{
    Console.WriteLine($"      {package} : {version}");
    try
    {
        await DotnetStarter.StartDotNet(
            arguments: $"add {project} package {package} -v {version}",
            directory: Directory.GetParent(project)!.FullName,
            timeout: 100000);
    }
    catch (Exception exception)
    {
        if (exception.Message.Contains(" is incompatible with "))
        {
            Console.WriteLine($"      Skipping due to incompatible TFM. {package} : {version}");
            Console.WriteLine(exception.Message);
            return;
        }

        throw;
    }
}


static Task Build(string solution)
{
    Console.WriteLine($"    Build {solution}");
    return DotnetStarter.StartDotNet(
        arguments: $"build {solution} --no-restore --nologo",
        directory: Directory.GetParent(solution)!.FullName,
        timeout: 20000);
}

static IEnumerable<string> ProjectFiles(string solutionDirectory) =>
    FileSystem.EnumerateFiles(solutionDirectory, "*.csproj")
        .Concat(FileSystem.EnumerateFiles(solutionDirectory, "*.fsproj"));
