public class ForkDetectorTests
{
    [Test]
    public async Task NoGitRepo_ShouldNotSkip()
    {
        using var temp = new TempDir();
        var solutionPath = Path.Combine(temp.Path, "test.sln");

        await Assert.That(ForkDetector.ShouldSkip(temp.Path, solutionPath)).IsFalse();
    }

    [Test]
    public async Task NotAFork_ShouldNotSkip()
    {
        using var temp = new TempDir();
        var repoDir = Path.Combine(temp.Path, "repo");
        CreateGitConfig(
            repoDir,
            """
            [core]
                repositoryformatversion = 0
            [remote "origin"]
                url = https://github.com/user/repo.git
                fetch = +refs/heads/*:refs/remotes/origin/*
            """);

        var solutionPath = Path.Combine(repoDir, "test.sln");

        await Assert.That(ForkDetector.ShouldSkip(temp.Path, solutionPath)).IsFalse();
    }

    [Test]
    public async Task Fork_DiscoveredViaScanning_ShouldSkip()
    {
        using var temp = new TempDir();
        var repoDir = Path.Combine(temp.Path, "forked-repo");
        CreateGitConfig(
            repoDir,
            """
            [core]
                repositoryformatversion = 0
            [remote "origin"]
                url = https://github.com/myuser/repo.git
                fetch = +refs/heads/*:refs/remotes/origin/*
            [remote "upstream"]
                url = https://github.com/original/repo.git
                fetch = +refs/heads/*:refs/remotes/upstream/*
            """);

        var solutionPath = Path.Combine(repoDir, "test.sln");

        // Target is the parent directory, fork was discovered via scanning
        await Assert.That(ForkDetector.ShouldSkip(temp.Path, solutionPath)).IsTrue();
    }

    [Test]
    public async Task Fork_ExplicitlyTargeted_ShouldNotSkip()
    {
        using var temp = new TempDir();
        var repoDir = Path.Combine(temp.Path, "forked-repo");
        CreateGitConfig(
            repoDir,
            """
            [core]
                repositoryformatversion = 0
            [remote "origin"]
                url = https://github.com/myuser/repo.git
                fetch = +refs/heads/*:refs/remotes/origin/*
            [remote "upstream"]
                url = https://github.com/original/repo.git
                fetch = +refs/heads/*:refs/remotes/upstream/*
            """);

        var solutionPath = Path.Combine(repoDir, "test.sln");

        // Target is the repo root itself, fork is explicitly targeted
        await Assert.That(ForkDetector.ShouldSkip(repoDir, solutionPath)).IsFalse();
    }

    [Test]
    public async Task Fork_TargetInsideRepo_ShouldNotSkip()
    {
        using var temp = new TempDir();
        var repoDir = Path.Combine(temp.Path, "forked-repo");
        var srcDir = Path.Combine(repoDir, "src");
        Directory.CreateDirectory(srcDir);
        CreateGitConfig(
            repoDir,
            """
            [core]
                repositoryformatversion = 0
            [remote "origin"]
                url = https://github.com/myuser/repo.git
                fetch = +refs/heads/*:refs/remotes/origin/*
            [remote "upstream"]
                url = https://github.com/original/repo.git
                fetch = +refs/heads/*:refs/remotes/upstream/*
            """);

        var solutionPath = Path.Combine(srcDir, "test.sln");

        // Target is inside the repo, fork is explicitly targeted
        await Assert.That(ForkDetector.ShouldSkip(srcDir, solutionPath)).IsFalse();
    }

    [Test]
    public async Task Fork_NoGitConfig_ShouldNotSkip()
    {
        using var temp = new TempDir();
        var repoDir = Path.Combine(temp.Path, "repo");
        var gitDir = Path.Combine(repoDir, ".git");
        Directory.CreateDirectory(gitDir);
        // No config file inside .git

        var solutionPath = Path.Combine(repoDir, "test.sln");

        await Assert.That(ForkDetector.ShouldSkip(temp.Path, solutionPath)).IsFalse();
    }

    [Test]
    public async Task SolutionInSubdirectory_Fork_ShouldSkip()
    {
        using var temp = new TempDir();
        var repoDir = Path.Combine(temp.Path, "forked-repo");
        var srcDir = Path.Combine(repoDir, "src", "MyProject");
        Directory.CreateDirectory(srcDir);
        CreateGitConfig(
            repoDir,
            """
            [core]
                repositoryformatversion = 0
            [remote "origin"]
                url = https://github.com/myuser/repo.git
            [remote "upstream"]
                url = https://github.com/original/repo.git
            """);

        var solutionPath = Path.Combine(srcDir, "test.sln");

        // Target is above the git root, fork discovered via scanning
        await Assert.That(ForkDetector.ShouldSkip(temp.Path, solutionPath)).IsTrue();
    }

    static void CreateGitConfig(string repoDir, string configContent)
    {
        var gitDir = Path.Combine(repoDir, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "config"), configContent);
    }

    sealed class TempDir :
        IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "PackageUpdateTests",
                Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public void Dispose() =>
            Directory.Delete(Path, true);
    }
}
