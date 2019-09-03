using Xunit;
using Xunit.Abstractions;

public class IntegrationTests :
    XunitLoggingBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public void ThisSolution()
    {
        Program.Inner(GitRepoDirectoryFinder.Find());
    }

    public IntegrationTests(ITestOutputHelper output) :
        base(output)
    {
    }
}