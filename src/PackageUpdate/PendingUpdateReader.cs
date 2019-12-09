using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

static class PendingUpdateReader
{
    public static async Task<List<PendingUpdate>> ReadPendingUpdates(string file)
    {
        var directory = Directory.GetParent(file).FullName;
        var lines = await DotnetStarter.StartDotNet($"list {file} package --outdated", directory);
        return ParseWithUpdates(lines).ToList();
    }

    public static IEnumerable<PendingUpdate> ParseWithUpdates(List<string> lines)
    {
        return ParseUpdates(lines).Where(StableOrWithPreRelease);
    }

    static bool StableOrWithPreRelease(PendingUpdate update)
    {
        var resolvedIsStable = !update.Resolved.Contains('-');
        var latestIsStable = !update.Latests.Contains('-');
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

            var split = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var package = split[1];
            var resolved = split[3];
            var latests = split[4];
            yield return new PendingUpdate
            (
                package: package,
                resolved: resolved,
                latests: latests
            );
        }
    }
}