// A linked git worktree (`git worktree add`, or a Claude Code session's .claude/worktrees/<name>)
// is a second checkout of a repo, usually on a branch someone is part way through. Updating it
// when it is found by scanning duplicates the update to the main checkout and leaves changes on
// that branch, so it is skipped unless explicitly targeted, as forks are
static class WorktreeDetector
{
    static ConcurrentDictionary<string, bool> cache = new(StringComparer.OrdinalIgnoreCase);

    public static bool ShouldSkip(string targetDirectory, string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        var gitRoot = FileSystem.FindGitRoot(directory);
        if (gitRoot == null)
        {
            return false;
        }

        if (!cache.GetOrAdd(gitRoot, IsWorktree))
        {
            return false;
        }

        return !FileSystem.IsTargeted(targetDirectory, gitRoot);
    }

    // A worktree's .git is a file holding `gitdir: <repo>/.git/worktrees/<name>`. A submodule's
    // .git is also a file, but points into .git/modules instead, so is not a worktree
    static bool IsWorktree(string gitRoot)
    {
        var gitFile = Path.Combine(gitRoot, ".git");
        if (!File.Exists(gitFile))
        {
            return false;
        }

        foreach (var line in File.ReadLines(gitFile))
        {
            if (!line.StartsWith("gitdir:", StringComparison.Ordinal))
            {
                continue;
            }

            var gitDir = Path.TrimEndingDirectorySeparator(line["gitdir:".Length..].Trim());
            var parent = Path.GetFileName(Path.GetDirectoryName(gitDir));
            return string.Equals(parent, "worktrees", StringComparison.Ordinal);
        }

        return false;
    }
}
