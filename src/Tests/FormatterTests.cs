public class FormatterTests
{
    [Test]
    public async Task Milliseconds() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromMilliseconds(500))).IsEqualTo("500ms");

    [Test]
    public async Task MillisecondsZero() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.Zero)).IsEqualTo("0ms");

    [Test]
    public async Task MillisecondsRounded() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromMilliseconds(99.7))).IsEqualTo("100ms");

    [Test]
    public async Task Seconds() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromSeconds(2.34))).IsEqualTo("2.3s");

    [Test]
    public async Task SecondsAtBoundary() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromSeconds(1))).IsEqualTo("1.0s");

    [Test]
    public async Task Minutes() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromMinutes(3.5))).IsEqualTo("3m30s");

    [Test]
    public async Task MinutesAtBoundary() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromMinutes(1))).IsEqualTo("1m0s");

    [Test]
    public async Task Hours() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromHours(2.5))).IsEqualTo("2h30m");

    [Test]
    public async Task HoursAtBoundary() =>
        await Assert.That(Formatter.FormatElapsed(TimeSpan.FromHours(1))).IsEqualTo("1h0m");
}