static class PendingUpdateReader
{
    public static async Task<List<PendingUpdate>> ReadPendingUpdates(string file)
    {
        var directory = Directory.GetParent(file)!.FullName;
        var lines = await DotnetStarter.StartDotNet(
            $"list {file} package --outdated",
            directory,
            timeout: 100000);
        return ParseWithUpdates(lines).ToList();
    }

    public static IEnumerable<PendingUpdate> ParseWithUpdates(List<string> lines) =>
        ParseUpdates(lines)
            .Where(_ => _.Latest != _.Resolved)
            .Where(StableOrWithPreRelease);

    static bool StableOrWithPreRelease(PendingUpdate update)
    {
        if (update.IsDeprecated)
        {
            return false;
        }
        var resolvedIsStable = !update.Resolved.Contains('-');
        var latestIsStable = !update.Latest.Contains('-');
        return !resolvedIsStable || latestIsStable;
    }

    public static IEnumerable<PendingUpdate> ParseUpdates(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (!line.StartsWith("   >"))
            {
                continue;
            }

            yield return ParseLine(line);
        }
    }

    static PendingUpdate ParseLine(string line)
    {
        var split = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var package = split[1];
        var resolved = split[3];
        var latest = split[4];
        var isDeprecated = line.EndsWith("(D)");
        return new(package, resolved, latest, isDeprecated);
    }
}