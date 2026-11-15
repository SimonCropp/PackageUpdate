static class Logging
{
    public static string LogsDirectory { get; } = Path.Combine(AssemblyLocation.CurrentDirectory, "logs");

    public static ILoggerFactory Init()
    {
        Console.Write($"Logs Directory: {LogsDirectory}");
        Directory.CreateDirectory(LogsDirectory);
        var configuration = new LoggerConfiguration();
        configuration.MinimumLevel.Debug();
        configuration.WriteTo.Console();
        configuration.WriteTo.File(
            Path.Combine(LogsDirectory, "log.txt"),
            rollOnFileSizeLimit: true,
            fileSizeLimitBytes: 1000000, //1mb
            retainedFileCountLimit: 10);
        Log.Logger = configuration.CreateLogger();
        return new SerilogLoggerFactory(Log.Logger, dispose: true);
    }
}