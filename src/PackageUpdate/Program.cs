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

        foreach (var solution in Directory.EnumerateFiles(targetDirectory, "*.sln", SearchOption.AllDirectories))
        {
            var solutionDir = Directory.GetParent(solution).FullName;
            Console.WriteLine($"  {solutionDir}");
            using (DotnetStarter.StartDotNet("restore", solutionDir))
            {
            }

            foreach (var project in Directory.EnumerateFiles(targetDirectory, "*.csproj", SearchOption.AllDirectories))
            {
                var directory = Directory.GetParent(project).FullName;
                Console.WriteLine($"    {directory}");
                foreach (var pendingUpdate in PendingUpdateReader.ReadPendingUpdates(directory))
                {
                    Console.WriteLine($"      {pendingUpdate.Package} : {pendingUpdate.Version}");
                    Update(project, pendingUpdate.Package, pendingUpdate.Version);
                }
            }
        }
    }

    static void Update(string project, string package, string version)
    {
        using (DotnetStarter.StartDotNet(
            arguments: $"add {project} package {package} -v {version}",
            directory: Directory.GetParent(project).FullName))
        {
        }
    }
}