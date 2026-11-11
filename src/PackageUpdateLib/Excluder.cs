static class Excluder
{
    static List<string> ignores;

    static Excluder()
    {
        var variable = Environment.GetEnvironmentVariable("PackageUpdateIgnores");
        if (variable == null)
        {
            ignores = [];
            return;
        }

        ignores = variable.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public static bool ShouldExclude(string solution) =>
        ignores.Any(_ => solution.Contains(_, StringComparison.OrdinalIgnoreCase));
}