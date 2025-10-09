public class SolutionRestoreTests
{
    [Fact]
    public Task ThisSolution()
    {
        var file = Path.Combine(GitRepoDirectoryFinder.Find(), "src", "PackageUpdate.slnx");
        return SolutionRestore.Run(file);
    }
}