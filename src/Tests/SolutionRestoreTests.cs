using System.IO;
using Xunit;
using Xunit.Abstractions;

public class SolutionRestoreTests :
    XunitLoggingBase
{
    [Fact]
    public void ThisSolution()
    {
        var file = Path.Combine(GitRepoDirectoryFinder.Find(), "src","PackageUpdate");
        SolutionRestore.Run(file);
    }

    public SolutionRestoreTests(ITestOutputHelper output) :
        base(output)
    {
    }
}