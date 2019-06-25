using System;
using System.Collections.Generic;
using System.Linq;

static class PendingUpdateReader
{
    public static List<PendingUpdate> ReadPendingUpdates(string directory)
    {
        var lines = DotnetStarter.StartDotNet("list package --outdated", directory);
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
            {
                Package = package,
                Version = version
            };
        }
    }
}