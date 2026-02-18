public class ExcluderTests
{
    [Test]
    public async Task Simple()
    {
        Environment.SetEnvironmentVariable("PackageUpdateIgnores", "ignore, otherIgnore");
        await Assert.That(Excluder.ShouldExclude("SolutionToIgnore.sln")).IsTrue();
        await Assert.That(Excluder.ShouldExclude("SolutionOtherIgnore.sln")).IsTrue();
        await Assert.That(Excluder.ShouldExclude("Solution.sln")).IsFalse();
    }
}