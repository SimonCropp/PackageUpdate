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

        ignores = variable.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public static bool ShouldExclude(string solution)
    {
        return ignores.Any(x => solution.Contains(x, StringComparison.OrdinalIgnoreCase));
    }
}