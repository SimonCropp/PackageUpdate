static class SharedLookup
{
    /// <summary>
    /// The lookup for the key: started by the first caller, and shared with every caller after, even
    /// while it is still in progress. So a lookup started ahead of time by <see cref="Prefetch"/> is
    /// awaited rather than made again. One that fails is not kept, so a later call makes it again, as
    /// it would if lookups were not shared.
    /// </summary>
    public static Task<TValue> GetOrStart<TKey, TValue>(
        this ConcurrentDictionary<TKey, Lazy<Task<TValue>>> lookups,
        TKey key,
        Func<Task<TValue>> start)
        where TKey : notnull
    {
        if (lookups.TryGetValue(key, out var existing))
        {
            return existing.Value;
        }

        Lazy<Task<TValue>>? lookup = null;
        lookup = new(async () =>
        {
            try
            {
                return await start();
            }
            catch
            {
                // Only this lookup, not one a later call has since added
                lookups.TryRemove(new(key, lookup!));
                throw;
            }
        });

        // Only the lookup that ends up in the dictionary is started
        return lookups.GetOrAdd(key, lookup).Value;
    }
}
