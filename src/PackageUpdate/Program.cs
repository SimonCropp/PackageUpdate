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

    foreach (var solution in FileSystem.EnumerateFiles(targetDirectory, "*.slnx"))
    {
        await TryProcessSolution(solution, package, build);
    }

    await Shutdown();
}

static Task Shutdown()
{
    Console.WriteLine("Shutdown dotnet build");
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
    var projects = ProjectFiles(solutionDirectory);

    if (File.Exists(Path.Combine(solutionDirectory, "Directory.Packages.props")))
    {
        Console.WriteLine("    Found Directory.Packages.props to processing only central packages");
        await UpdateCentral(package, projects, solutionDirectory);
    }
    else
    {
        await Update(package, projects, solutionDirectory);
    }

    if (build)
    {
        await Build(solution);
    }
}

static async Task UpdateCentral(string? targetPackage, IEnumerable<string> projects, string solutionDirectory)
{
    if (targetPackage == null)
    {
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projects)
        {
            WriteProject(project, solutionDirectory);
            foreach (var pending in await PendingUpdateReader.ReadPendingUpdates(project))
            {
                var package = pending.Package;
                if (processed.Contains(package))
                {
                    Console.WriteLine($"        Skipping {package} since already processed");
                    continue;
                }

                await Add(project, package, pending.Latest);
                processed.Add(package);
            }
        }
    }
    else
    {
        foreach (var project in projects)
        {
            WriteProject(project, solutionDirectory);
            foreach (var pending in await PendingUpdateReader.ReadPendingUpdates(project))
            {
                if (!string.Equals(targetPackage, pending.Package, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await Add(project, pending.Package, pending.Latest);
                return;
            }
        }
    }
}

static async Task Update(string? package, IEnumerable<string> projects, string solutionDirectory)
{
    if (package == null)
    {
        foreach (var project in projects)
        {
            WriteProject(project, solutionDirectory);
            foreach (var pending in await PendingUpdateReader.ReadPendingUpdates(project))
            {
                await Add(project, pending.Package, pending.Latest);
            }
        }
    }
    else
    {
        foreach (var project in projects)
        {
            WriteProject(project, solutionDirectory);
            foreach (var pending in await PendingUpdateReader.ReadPendingUpdates(project))
            {
                if (!string.Equals(package, pending.Package, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await Add(project, pending.Package, pending.Latest);
            }
        }
    }
}

static async Task Add(string project, string package, string version)
{
    Console.WriteLine($"      {package} : {version}");
    try
    {
        await DotnetStarter.StartDotNet(
            arguments: $"add {project} package {package} -v {version}",
            directory: Directory.GetParent(project)!.FullName,
            timeout: 100000);
    }
    catch (Exception exception) when (exception.Message.Contains(" is incompatible with "))
    {
        Console.WriteLine($"    Skipping due to incompatible TFM. {package} : {version}");
        Console.WriteLine(exception.Message);
    }
}

static Task Build(string solution)
{
    Console.WriteLine($"    Build {solution}");
    return DotnetStarter.StartDotNet(
        arguments: $"build {solution} --no-restore --nologo",
        directory: Directory.GetParent(solution)!.FullName,
        timeout: 0);
}

static IEnumerable<string> ProjectFiles(string solutionDirectory) =>
    FileSystem.EnumerateFiles(solutionDirectory, "*.csproj")
        .Concat(FileSystem.EnumerateFiles(solutionDirectory, "*.fsproj"));

static void WriteProject(string project, string solutionDirectory) =>
    Console.WriteLine($"    {project.Replace(solutionDirectory, "").Trim(Path.DirectorySeparatorChar)}");
