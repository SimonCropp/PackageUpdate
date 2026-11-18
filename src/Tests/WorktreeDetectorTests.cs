public class WorktreeDetectorTests
{
    [Test]
    public async Task NoGitRepo_ShouldNotSkip()
    {
        using var temp = new TempDirectory();
        var solutionPath = Path.Combine(temp, "test.sln");

        await Assert.That(WorktreeDetector.ShouldSkip(temp, solutionPath)).IsFalse();
    }

    [Test]
    public async Task MainCheckout_ShouldNotSkip()
    {
        using var temp = new TempDirectory();
        var repoDir = Path.Combine(temp, "repo");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));

        var solutionPath = Path.Combine(repoDir, "src", "test.sln");

        await Assert.That(WorktreeDetector.ShouldSkip(temp, solutionPath)).IsFalse();
    }

    [Test]
    public async Task NestedWorktree_DiscoveredViaScanning_ShouldSkip()
    {
        using var temp = new TempDirectory();
        var repoDir = Path.Combine(temp, "repo");
        var worktreeDir = CreateWorktree(repoDir, Path.Combine(repoDir, ".claude", "worktrees", "feature"));

        var solutionPath = Path.Combine(worktreeDir, "src", "test.slnx");

        // Target is the main checkout, so the worktree inside it was found by scanning
        await Assert.That(WorktreeDetector.ShouldSkip(repoDir, solutionPath)).IsTrue();
    }

    [Test]
    public async Task SiblingWorktree_DiscoveredViaScanning_ShouldSkip()
    {
        using var temp = new TempDirectory();
        var repoDir = Path.Combine(temp, "repo");
        var worktreeDir = CreateWorktree(repoDir, Path.Combine(temp, "repo-feature"));

        var solutionPath = Path.Combine(worktreeDir, "test.sln");

        await Assert.That(WorktreeDetector.ShouldSkip(temp, solutionPath)).IsTrue();
    }

    [Test]
    public async Task Worktree_ExplicitlyTargeted_ShouldNotSkip()
    {
        using var temp = new TempDirectory();
        var repoDir = Path.Combine(temp, "repo");
        var worktreeDir = CreateWorktree(repoDir, Path.Combine(repoDir, ".claude", "worktrees", "feature"));

        var solutionPath = Path.Combine(worktreeDir, "src", "test.slnx");

        await Assert.That(WorktreeDetector.ShouldSkip(worktreeDir, solutionPath)).IsFalse();
    }

    [Test]
    public async Task Worktree_TargetInsideWorktree_ShouldNotSkip()
    {
        using var temp = new TempDirectory();
        var repoDir = Path.Combine(temp, "repo");
        var worktreeDir = CreateWorktree(repoDir, Path.Combine(repoDir, ".claude", "worktrees", "feature"));
        var srcDir = Path.Combine(worktreeDir, "src");

        var solutionPath = Path.Combine(srcDir, "test.slnx");

        await Assert.That(WorktreeDetector.ShouldSkip(srcDir, solutionPath)).IsFalse();
    }

    [Test]
    public async Task Worktree_RelativeGitDir_ShouldSkip()
    {
        using var temp = new TempDirectory();
        var repoDir = Path.Combine(temp, "repo");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git", "worktrees", "feature"));
        var worktreeDir = Path.Combine(repoDir, ".claude", "worktrees", "feature");
        Directory.CreateDirectory(worktreeDir);
        // Written by git when worktree.useRelativePaths is set
        await File.WriteAllTextAsync(Path.Combine(worktreeDir, ".git"), "gitdir: ../../../.git/worktrees/feature\n");

        var solutionPath = Path.Combine(worktreeDir, "test.slnx");

        await Assert.That(WorktreeDetector.ShouldSkip(repoDir, solutionPath)).IsTrue();
    }

    [Test]
    public async Task Submodule_ShouldNotSkip()
    {
        using var temp = new TempDirectory();
        var repoDir = Path.Combine(temp, "repo");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git", "modules", "lib"));
        var submoduleDir = Path.Combine(repoDir, "lib");
        Directory.CreateDirectory(submoduleDir);
        await File.WriteAllTextAsync(Path.Combine(submoduleDir, ".git"), "gitdir: ../.git/modules/lib\n");

        var solutionPath = Path.Combine(submoduleDir, "test.sln");

        await Assert.That(WorktreeDetector.ShouldSkip(repoDir, solutionPath)).IsFalse();
    }

    // Lays out a worktree as `git worktree add` does
    static string CreateWorktree(string repoDir, string worktreeDir)
    {
        var worktreeGitDir = Path.Combine(repoDir, ".git", "worktrees", Path.GetFileName(worktreeDir));
        Directory.CreateDirectory(worktreeGitDir);
        Directory.CreateDirectory(worktreeDir);
        var gitDir = worktreeGitDir.Replace('\\', '/');
        File.WriteAllText(Path.Combine(worktreeDir, ".git"), $"gitdir: {gitDir}\n");
        return worktreeDir;
    }
}
