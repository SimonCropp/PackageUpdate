public class FormatterTests
{
    [Fact]
    public void Milliseconds() =>
        Assert.Equal("500ms", Formatter.FormatElapsed(TimeSpan.FromMilliseconds(500)));

    [Fact]
    public void MillisecondsZero() =>
        Assert.Equal("0ms", Formatter.FormatElapsed(TimeSpan.Zero));

    [Fact]
    public void MillisecondsRounded() =>
        Assert.Equal("100ms", Formatter.FormatElapsed(TimeSpan.FromMilliseconds(99.7)));

    [Fact]
    public void Seconds() =>
        Assert.Equal("2.3s", Formatter.FormatElapsed(TimeSpan.FromSeconds(2.34)));

    [Fact]
    public void SecondsAtBoundary() =>
        Assert.Equal("1.0s", Formatter.FormatElapsed(TimeSpan.FromSeconds(1)));

    [Fact]
    public void Minutes() =>
        Assert.Equal("3m30s", Formatter.FormatElapsed(TimeSpan.FromMinutes(3.5)));

    [Fact]
    public void MinutesAtBoundary() =>
        Assert.Equal("1m0s", Formatter.FormatElapsed(TimeSpan.FromMinutes(1)));

    [Fact]
    public void Hours() =>
        Assert.Equal("2h30m", Formatter.FormatElapsed(TimeSpan.FromHours(2.5)));

    [Fact]
    public void HoursAtBoundary() =>
        Assert.Equal("1h0m", Formatter.FormatElapsed(TimeSpan.FromHours(1)));
}
