using System;
using System.IO;
using CommandLine;

static class CommandRunner
{
    public static void RunCommand(Invoke invoke, params string[] args)
    {
        if (args.Length == 1)
        {
            var firstArg = args[0];
            if (!firstArg.StartsWith('-'))
            {
                invoke(firstArg, null);
                return;
            }
        }

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(
                options =>
                {
                    var targetDirectory = FindTargetDirectory(options.TargetDirectory);
                    invoke(targetDirectory, options.Package);
                });
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