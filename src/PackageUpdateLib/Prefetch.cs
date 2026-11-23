/// <summary>
/// Starts lookups ahead of a loop that goes through the same items in order, a bounded number at a
/// time, so their requests overlap rather than each waiting on the last. The lookups are shared via
/// <see cref="SharedLookup"/>, so the loop awaits the one started here rather than making its own.
/// Nothing is changed or reported here: the loop still does that, in order, so the result and the
/// progress are the same as without it.
/// </summary>
sealed class Prefetch : IAsyncDisposable
{
    // Enough for the requests to overlap, without flooding a private feed
    internal const int MaxConcurrentLookups = 16;

    CancellationTokenSource cancel = new();
    Task running = Task.CompletedTask;

    public static Prefetch Start<T>(IEnumerable<T> items, Func<T, Task> lookup)
    {
        var prefetch = new Prefetch();
        prefetch.running = Run(items, lookup, prefetch.cancel.Token);
        return prefetch;
    }

    static async Task Run<T>(IEnumerable<T> items, Func<T, Task> lookup, Cancel cancel)
    {
        try
        {
            await Parallel.ForEachAsync(
                items,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxConcurrentLookups,
                    CancellationToken = cancel
                },
                async (item, _) =>
                {
                    try
                    {
                        await lookup(item);
                    }
                    catch
                    {
                        // Left for the loop. A failed lookup is not kept, so the loop makes it again,
                        // and fails on it, or not, in order. The rest are still started
                    }
                });
        }
        catch
        {
            // Stopped, or the items could not be read. The loop makes any lookup not started here
        }
    }

    /// <summary>
    /// Stops starting lookups, and waits for those already started, so none outlive the
    /// <see cref="SourceCacheContext"/> they use.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Cancel rather than CancelAsync, so no further lookup starts once this returns
        cancel.Cancel();
        await running;
        cancel.Dispose();
    }
}
