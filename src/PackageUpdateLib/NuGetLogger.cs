using NuGetLogLevel = NuGet.Common.LogLevel;
#pragma warning disable CA2254

// Forwards NuGet's logging to the ILogger given to PackageUpdater.Run
class NuGetLogger(ILogger logger) :
    NuGet.Common.ILogger
{
    public void LogDebug(string data) =>
        logger.LogDebug(data);

    public void LogVerbose(string data) =>
        logger.LogTrace(data);

    public void LogInformation(string data) =>
        logger.LogInformation(data);

    public void LogMinimal(string data) =>
        logger.LogInformation(data);

    public void LogWarning(string data) =>
        logger.LogWarning(data);

    public void LogError(string data) =>
        logger.LogError(data);

    public void LogInformationSummary(string data) =>
        logger.LogInformation(data);

    public void Log(NuGetLogLevel level, string data) =>
        logger.Log(MapLogLevel(level), data);

    public Task LogAsync(NuGetLogLevel level, string data)
    {
        Log(level, data);
        return Task.CompletedTask;
    }

    public void Log(ILogMessage message)
    {
        var level = MapLogLevel(message.Level);
        if (!logger.IsEnabled(level))
        {
            return;
        }

        var properties = new Dictionary<string, object>();

        if (message.Code != NuGetLogCode.Undefined)
        {
            properties["NuGetCode"] = message.Code;
        }

        if (!string.IsNullOrEmpty(message.ProjectPath))
        {
            properties["ProjectPath"] = message.ProjectPath;
        }

        if (message.WarningLevel > WarningLevel.Minimal)
        {
            properties["WarningLevel"] = (int) message.WarningLevel;
        }

        using (logger.BeginScope(properties))
        {
            logger.Log(level, message.Message);
        }
    }

    public Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    static LogLevel MapLogLevel(NuGetLogLevel level) =>
        level switch
        {
            NuGetLogLevel.Debug => LogLevel.Debug,
            NuGetLogLevel.Verbose => LogLevel.Trace,
            NuGetLogLevel.Warning => LogLevel.Warning,
            NuGetLogLevel.Error => LogLevel.Error,
            _ => LogLevel.Information
        };
}
