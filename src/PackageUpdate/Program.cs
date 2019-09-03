using System;
using System.IO;

static class Program
{
    static void Main(string[] args)
    {
        CommandRunner.RunCommand(Inner, args);
    }
    
    static void Inner(string targetDirectory, string package)
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
            TryProcessSolution(solution, package);
        }
    }

    static void TryProcessSolution(string solution, string package)
    {
        try
        {
            ProcessSolution(solution, package);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($@"Failed to process solution: {solution}.
Error: {e.Message}");
            Console.ResetColor();
        }
    }

    static void ProcessSolution(string solution, string package)
    {
        if (Excluder.ShouldExclude(solution))
        {
            Console.WriteLine($"  Exclude: {solution}");
            return;
        }

        Console.WriteLine($"  {solution}");
        SolutionRestore.Run(solution);

        var solutionDirectory = Directory.GetParent(solution).FullName;
        foreach (var project in FileSystem.EnumerateFiles(solutionDirectory, "*.csproj"))
        {
            Console.WriteLine($"    {project.Replace(solutionDirectory, "").Trim(Path.DirectorySeparatorChar)}");
            foreach (var pending in PendingUpdateReader.ReadPendingUpdates(project))
            {
                if (package == null)
                {
                    Update(project, pending.Package, pending.Version);
                    continue;
                }

                if (string.Equals(package, pending.Package, StringComparison.OrdinalIgnoreCase))
                {
                    Update(project, pending.Package, pending.Version);
                }
            }
        }
    }

    static void Update(string project, string package, string version)
    {
        Console.WriteLine($"      {package} : {version}");
        DotnetStarter.StartDotNet(
            arguments: $"add {project} package {package} -v {version}",
            directory: Directory.GetParent(project).FullName);
    }
}