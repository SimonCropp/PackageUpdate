static class Formatter
{
    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int) elapsed.TotalHours}h{elapsed.Minutes}m";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int) elapsed.TotalMinutes}m{elapsed.Seconds}s";
        }

        if (elapsed.TotalSeconds >= 1)
        {
            return $"{elapsed.TotalSeconds:0.0}s";
        }

        return $"{elapsed.TotalMilliseconds:0}ms";
    }
}
