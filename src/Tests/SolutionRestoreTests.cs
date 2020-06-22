using System.IO;
using System.Threading.Tasks;
using Xunit;

public class SolutionRestoreTests
{
    [Fact]
    public Task ThisSolution()
    {
        var file = Path.Combine(GitRepoDirectoryFinder.Find(), "src", "PackageUpdate.sln");
        return SolutionRestore.Run(file);
    }
}