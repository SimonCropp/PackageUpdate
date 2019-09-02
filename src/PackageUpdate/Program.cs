using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        CommandRunner.RunCommand(Inner, args);
    }

    static void Inner(string targetDirectory)
    {
        Console.WriteLine($"TargetDirectory: {targetDirectory}");
        if (!Directory.Exists(targetDirectory))
        {
            Console.WriteLine($"Target directory does not exist: {targetDirectory}");
            Environment.Exit(1);
        }

        foreach (var solution in FileSystem.EnumerateFiles(targetDirectory, "*.sln"))
        {
            TryProcessSolution(solution);
        }
    }

    static void TryProcessSolution(string solution)
    {
        try
        {
            ProcessSolution(solution);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($@"Failed to process solution: {solution}.
Error: {e.Message}");
            Console.ResetColor();
        }
    }
    
    static void ProcessSolution(string solution)
    {
        Console.WriteLine($"  {solution}");
        SolutionRestore.Run(solution);

        var solutionDirectory = Directory.GetParent(solution).FullName;
        foreach (var project in FileSystem.EnumerateFiles(solutionDirectory, "*.csproj"))
        {
            var directory = Directory.GetParent(project).FullName;
            Console.WriteLine($"    {directory.Replace(solutionDirectory,"").Trim(Path.DirectorySeparatorChar)}");
            foreach (var pendingUpdate in PendingUpdateReader.ReadPendingUpdates(directory))
            {
                Console.WriteLine($"      {pendingUpdate.Package} : {pendingUpdate.Version}");
                Update(project, pendingUpdate.Package, pendingUpdate.Version);
            }
        }
    }

    static void Update(string project, string package, string version)
    {
        DotnetStarter.StartDotNet(
            arguments: $"add {project} package {package} -v {version}",
            directory: Directory.GetParent(project).FullName);
    }
}