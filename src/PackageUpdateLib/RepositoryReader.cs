public static class RepositoryReader
{
    // Shared, since lookups for many packages start at once and each needs its source's repository
    static ConcurrentDictionary<PackageSource, Lazy<Task<(SourceRepository, PackageMetadataResource)>>> cache = [];
    static Repository.RepositoryFactory factory = Repository.Factory;

    public static Task<(SourceRepository repository, PackageMetadataResource metadataResource)> Read(PackageSource source) =>
        cache.GetOrStart(source, () => Create(source));

    static async Task<(SourceRepository, PackageMetadataResource)> Create(PackageSource source)
    {
        var repository = factory.GetCoreV3(source);

        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>() ??
                               throw new($"Could not resolve a {nameof(PackageMetadataResource)} for source {source.Source}");
        return (repository, metadataResource);
    }
}
