using var loggerFactory = Logging.Init();
var logger = loggerFactory.CreateLogger("PackageUpdate");
await CommandRunner.RunCommand(
    (directory, package, build) => PackageUpdater.Run(directory, package, build, null, logger),
    args);
