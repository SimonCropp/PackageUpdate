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
        return ParseUpdates(lines).ToList();
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
            var version = split[4];
            yield return new PendingUpdate
            (
                package: package,
                version: version
            );
        }
    }
}