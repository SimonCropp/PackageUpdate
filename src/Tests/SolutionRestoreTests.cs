using System.IO;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

public class SolutionRestoreTests :
    VerifyBase
{
    [Fact]
    public void ThisSolution()
    {
        var file = Path.Combine(GitRepoDirectoryFinder.Find(), "src", "PackageUpdate.sln");
        SolutionRestore.Run(file);
    }

    public SolutionRestoreTests(ITestOutputHelper output) :
        base(output)
    {
    }
}