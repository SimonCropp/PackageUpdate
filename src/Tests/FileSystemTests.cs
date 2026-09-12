public class FileSystemTests
{
    [Test]
    public async Task FindPropsFile_PropsBesideSolution_ReturnsPropsPath()
    {
        using var temp = new TempDir();
        var solutionDir = PropsHelper.CreateSolutionDir(temp.Path, "repo");
        var propsPath = await PropsHelper.CreateProps(solutionDir);

        var result = FileSystem.FindPropsFile(solutionDir, temp.Path);

        await Assert.That(result).IsEqualTo(propsPath);
    }

    [Test]
    public async Task FindPropsFile_PropsInAncestorDir_WalksUpToTarget()
    {
        using var temp = new TempDir();
        var solutionDir = PropsHelper.CreateSolutionDir(temp.Path, Path.Combine("repo", "src", "App"));
        var propsPath = await PropsHelper.CreateProps(temp.Path);

        var result = FileSystem.FindPropsFile(solutionDir, temp.Path);

        await Assert.That(result).IsEqualTo(propsPath);
    }

    [Test]
    public async Task FindPropsFile_NoProps_ReturnsNull()
    {
        using var temp = new TempDir();
        var solutionDir = PropsHelper.CreateSolutionDir(temp.Path, Path.Combine("repo", "src", "App"));

        var result = FileSystem.FindPropsFile(solutionDir, temp.Path);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindPropsFile_PropsAboveTargetDir_ReturnsNull()
    {
        using var temp = new TempDir();
        var targetDir = Path.Combine(temp.Path, "repo");
        var solutionDir = PropsHelper.CreateSolutionDir(targetDir, "src");
        // Props sits above the target directory, so the search must stop before reaching it
        await PropsHelper.CreateProps(temp.Path);

        var result = FileSystem.FindPropsFile(solutionDir, targetDir);

        await Assert.That(result).IsNull();
    }
}
