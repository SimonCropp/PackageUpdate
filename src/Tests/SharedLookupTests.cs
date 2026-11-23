public class SharedLookupTests
{
    [Test]
    public async Task LookupInProgressIsShared()
    {
        var lookups = new ConcurrentDictionary<string, Lazy<Task<int>>>();
        var starts = 0;
        var finish = new TaskCompletionSource<int>();

        Task<int> Start()
        {
            Interlocked.Increment(ref starts);
            return finish.Task;
        }

        var first = lookups.GetOrStart("key", Start);
        var second = lookups.GetOrStart("key", Start);
        finish.SetResult(42);

        await Assert.That(await first).IsEqualTo(42);
        await Assert.That(await second).IsEqualTo(42);
        await Assert.That(starts).IsEqualTo(1);
    }

    [Test]
    public async Task FinishedLookupIsKept()
    {
        var lookups = new ConcurrentDictionary<string, Lazy<Task<int>>>();
        var starts = 0;

        Task<int> Start()
        {
            starts++;
            return Task.FromResult(starts);
        }

        await lookups.GetOrStart("key", Start);
        var again = await lookups.GetOrStart("key", Start);

        await Assert.That(again).IsEqualTo(1);
        await Assert.That(starts).IsEqualTo(1);
    }

    // As before lookups were shared, a failure is not kept, so a later call tries again
    [Test]
    public async Task FailedLookupIsNotKept()
    {
        var lookups = new ConcurrentDictionary<string, Lazy<Task<int>>>();
        var starts = 0;

        async Task<int> Start()
        {
            starts++;
            await Task.Yield();
            if (starts == 1)
            {
                throw new("Expected failure");
            }

            return 42;
        }

        var failed = () => lookups.GetOrStart("key", Start);
        await Assert.That(failed).Throws<Exception>();

        var retried = await lookups.GetOrStart("key", Start);

        await Assert.That(retried).IsEqualTo(42);
        await Assert.That(starts).IsEqualTo(2);
    }
}
