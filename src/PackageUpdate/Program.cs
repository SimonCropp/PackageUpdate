using var loggerFactory = Logging.Init();
var logger = loggerFactory.CreateLogger("PackageUpdate");
await CommandRunner.RunCommand(
    async (directory, package, build) =>
    {
        var result = await PackageUpdater.Run(directory, package, build, null, logger);

        // A failed solution or tool manifest does not stop the rest, so it is only known here
        if (!result.Succeeded)
        {
            Environment.ExitCode = 1;
        }
    },
    args);
