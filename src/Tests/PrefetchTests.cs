public class PrefetchTests
{
    [Test]
    public async Task DisposeStopsStartingAndWaitsForStarted()
    {
        var started = 0;
        var finished = 0;
        var release = new TaskCompletionSource();

        var prefetch = Prefetch.Start(
            Enumerable.Range(0, Prefetch.MaxConcurrentLookups * 4),
            async _ =>
            {
                Interlocked.Increment(ref started);
                await release.Task;
                Interlocked.Increment(ref finished);
            });

        await WaitFor(() => Volatile.Read(ref started) == Prefetch.MaxConcurrentLookups);
        var disposing = prefetch.DisposeAsync().AsTask();

        await Assert.That(disposing.IsCompleted).IsFalse();

        release.SetResult();
        await disposing;

        await Assert.That(started).IsEqualTo(Prefetch.MaxConcurrentLookups);
        await Assert.That(finished).IsEqualTo(Prefetch.MaxConcurrentLookups);
    }

    [Test]
    public async Task FailedLookupDoesNotStopTheRest()
    {
        var finished = 0;
        const int count = 50;

        await using var prefetch = Prefetch.Start(
            Enumerable.Range(0, count),
            async _ =>
            {
                await Task.Yield();
                if (_ == 0)
                {
                    throw new("Expected failure");
                }

                Interlocked.Increment(ref finished);
            });

        await WaitFor(() => Volatile.Read(ref finished) == count - 1);
    }

    // Reading the items ahead of the loop can fail, such as on a csproj that is not valid xml. That
    // is left for the loop, which reads them again
    [Test]
    public async Task UnreadableItemsDoNotThrow()
    {
        static IEnumerable<int> Unreadable()
        {
            yield return 1;
            throw new("Expected failure");
        }

        var prefetch = Prefetch.Start(Unreadable(), _ => Task.CompletedTask);

        await prefetch.DisposeAsync();
    }

    static async Task WaitFor(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
            {
                throw new("Timed out");
            }

            await Task.Delay(10);
        }
    }
}
