using System.IO;
using System.Threading.Tasks;

static class SolutionRestore
{
    public static Task Run(string solution)
    {
        var solutionDirectory = Directory.GetParent(solution).FullName;
        return DotnetStarter.StartDotNet($"restore {solution}", solutionDirectory);
    }
}