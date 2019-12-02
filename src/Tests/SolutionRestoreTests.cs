using System.IO;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

public class SolutionRestoreTests :
    VerifyBase
{
    [Fact]
    public Task ThisSolution()
    {
        var file = Path.Combine(GitRepoDirectoryFinder.Find(), "src", "PackageUpdate.sln");
        return SolutionRestore.Run(file);
    }

    public SolutionRestoreTests(ITestOutputHelper output) :
        base(output)
    {
    }
}