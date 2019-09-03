using System;
using System.Collections.Generic;
using System.Linq;

static class Excluder
{
    static List<string> ignores;

    static Excluder()
    {
        var variable = Environment.GetEnvironmentVariable("PackageUpdateIgnores");
        if (variable == null)
        {
            ignores = new List<string>();
            return;
        }
        ignores = variable.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x=>x.Trim()).ToList();
    }
    public static bool ShouldExclude(string solution)
    {
        return ignores.Any(x => solution.Contains(x, StringComparison.OrdinalIgnoreCase));
    }
}