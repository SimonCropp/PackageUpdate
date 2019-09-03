using System;
using Xunit;
using Xunit.Abstractions;

public class ExcluderTests :
    XunitLoggingBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public void ThisSolution()
    {
        Environment.SetEnvironmentVariable("PackageUpdateIgnores", "ignore, otherIgnore");
        Assert.True(Excluder.ShouldExclude("SolutionToIgnore.sln"));
        Assert.True(Excluder.ShouldExclude("SolutionOtherIgnore.sln"));
        Assert.False(Excluder.ShouldExclude("Solution.sln"));
    }

    public ExcluderTests(ITestOutputHelper output) :
        base(output)
    {
    }
}