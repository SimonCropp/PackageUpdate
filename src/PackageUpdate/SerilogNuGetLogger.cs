using ILogger = NuGet.Common.ILogger;
#pragma warning disable CA2254

public class SerilogNuGetLogger : ILogger
{
    public static SerilogNuGetLogger Instance { get; } = new();

    public void LogDebug(string data) =>
        Serilog.Log.Debug(data);

    public void LogVerbose(string data) =>
        Serilog.Log.Verbose(data);

    public void LogInformation(string data) =>
        Serilog.Log.Information(data);

    public void LogMinimal(string data) =>
        Serilog.Log.Information(data);

    public void LogWarning(string data) =>
        Serilog.Log.Warning(data);

    public void LogError(string data) =>
        Serilog.Log.Error(data);

    public void LogInformationSummary(string data) =>
        Serilog.Log.Information(data);

    public void Log(LogLevel level, string data) =>
        Serilog.Log.Write(MapLogLevel(level), data);

    public Task LogAsync(LogLevel level, string data)
    {
        Log(level, data);
        return Task.CompletedTask;
    }

    public void Log(ILogMessage message)
    {
        var parser = new MessageTemplateParser();
        var messageTemplate = parser.Parse(message.Message);

        var properties = new List<LogEventProperty>();

        if (message.Code != NuGetLogCode.Undefined)
            properties.Add(new("NuGetCode", new ScalarValue(message.Code)));

        if (!string.IsNullOrEmpty(message.ProjectPath))
            properties.Add(new("ProjectPath", new ScalarValue(message.ProjectPath)));

        if (message.WarningLevel > WarningLevel.Minimal)
            properties.Add(new("WarningLevel", new ScalarValue((int) message.WarningLevel)));

        var logEvent = new LogEvent(
            timestamp: message.Time,
            level: MapLogLevel(message.Level),
            exception: null,
            messageTemplate: messageTemplate,
            properties: properties);

        Serilog.Log.Write(logEvent);
    }

    public Task LogAsync(ILogMessage message)
    {
        Log(message);
        return Task.CompletedTask;
    }

    static LogEventLevel MapLogLevel(LogLevel level) =>
        level switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Verbose => LogEventLevel.Verbose,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };
}