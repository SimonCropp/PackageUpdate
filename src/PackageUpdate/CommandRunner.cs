static class CommandRunner
{
    public static Task RunCommand(Invoke invoke, params string[] args)
    {
        if (args.Length == 1)
        {
            var firstArg = args[0];
            if (!firstArg.StartsWith('-'))
            {
                return invoke(firstArg, null, false);
            }
        }

        return Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(
                options =>
                {
                    var targetDirectory = FindTargetDirectory(options.TargetDirectory);
                    return invoke(targetDirectory, options.Package, options.Build);
                });
    }

    static async Task<ParserResult<T>> WithParsedAsync<T>(
        this ParserResult<T> result,
        Func<T, Task> action)
    {
        if (result is Parsed<T> parsed)
        {
            await action(parsed.Value);
        }

        return result;
    }

    static string FindTargetDirectory(string? targetDirectory)
    {
        if (targetDirectory == null)
        {
            return Environment.CurrentDirectory;
        }

        return Path.GetFullPath(targetDirectory);
    }
}