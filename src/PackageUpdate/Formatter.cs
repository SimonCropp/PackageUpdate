static class Formatter
{
    public static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? $"{(int) elapsed.TotalHours}h{elapsed.Minutes}m"
            : elapsed.TotalMinutes >= 1
                ? $"{(int) elapsed.TotalMinutes}m{elapsed.Seconds}s"
                : elapsed.TotalSeconds >= 1
                    ? $"{elapsed.TotalSeconds:0.0}s"
                    : $"{elapsed.TotalMilliseconds:0}ms";
}
