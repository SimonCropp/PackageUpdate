using System.IO;
using Xunit;
using Xunit.Abstractions;

public class IntegrationTests :
    XunitLoggingBase
{
    [Fact]
    public void ThisSolution()
    {
        Program.Inner(GitRepoDirectoryFinder.Find());
    }

    public IntegrationTests(ITestOutputHelper output) :
        base(output)
    {
    }
}