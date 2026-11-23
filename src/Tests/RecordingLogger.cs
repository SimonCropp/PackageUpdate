using Microsoft.Extensions.Logging;

// Keeps each message logged, so a test can check what was looked at. Lookups run concurrently, so
// messages can come from more than one thread at once
class RecordingLogger : ILogger
{
    Lock sync = new();
    List<string> messages = [];

    // A copy, so it can be read while lookups still log
    public List<string> Messages
    {
        get
        {
            lock (sync)
            {
                return [..messages];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;

    public bool IsEnabled(LogLevel logLevel) =>
        true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (sync)
        {
            messages.Add(message);
        }
    }
}
