﻿using System;
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
                invoke(firstArg);
                return;
            }
        }

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(
                options =>
                {
                    ApplyDefaults(options);
                    invoke(options.TargetDirectory);
                });
    }

    static void ApplyDefaults(Options options)
    {
        if (options.TargetDirectory == null)
        {
            options.TargetDirectory = Environment.CurrentDirectory;
        }
        else
        {
            options.TargetDirectory = Path.GetFullPath(options.TargetDirectory);
        }
    }
}