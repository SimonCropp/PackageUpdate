using System;
using Xunit;

public class ExcluderTests
{
    [Fact]
    public void Simple()
    {
        Environment.SetEnvironmentVariable("PackageUpdateIgnores", "ignore, otherIgnore");
        Assert.True(Excluder.ShouldExclude("SolutionToIgnore.sln"));
        Assert.True(Excluder.ShouldExclude("SolutionOtherIgnore.sln"));
        Assert.False(Excluder.ShouldExclude("Solution.sln"));
    }
}