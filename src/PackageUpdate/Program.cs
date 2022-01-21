await CommandRunner.RunCommand(Inner, args);

static async Task Inner(string targetDirectory, string? package)
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
        await TryProcessSolution(solution, package);
    }
}

static async Task TryProcessSolution(string solution, string? package)
{
    try
    {
        await ProcessSolution(solution, package);
    }
    catch (Exception e)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine($@"Failed to process solution: {solution}.
Error: {e.Message}");
        Console.ResetColor();
    }
}

static async Task ProcessSolution(string solution, string? package)
{
    if (Excluder.ShouldExclude(solution))
    {
        Console.WriteLine($"  Exclude: {solution}");
        return;
    }

    Console.WriteLine($"  {solution}");
    await SolutionRestore.Run(solution);

    var solutionDirectory = Directory.GetParent(solution)!.FullName;
    foreach (var project in FileSystem.EnumerateFiles(solutionDirectory, "*.csproj"))
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
}

static Task Update(string project, string package, string version)
{
    Console.WriteLine($"      {package} : {version}");
    return DotnetStarter.StartDotNet(
        arguments: $"add {project} package {package} -v {version}",
        directory: Directory.GetParent(project)!.FullName);
}
