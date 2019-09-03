using System.IO;

static class SolutionRestore
{
    public static void Run(string solution)
    {
        var solutionDirectory = Directory.GetParent(solution).FullName;
        DotnetStarter.StartDotNet($"restore {solution}", solutionDirectory);
    }
}