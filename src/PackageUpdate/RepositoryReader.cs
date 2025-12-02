public static class RepositoryReader
{
    static Dictionary<PackageSource, (SourceRepository, PackageMetadataResource)> cache = [];
    static Repository.RepositoryFactory factory = Repository.Factory;

    public static async Task<(SourceRepository repository, PackageMetadataResource metadataResource)> Read(PackageSource source)
    {
        if (!cache.TryGetValue(source, out var value))
        {
            var repository = factory.GetCoreV3(source);

            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
            cache[source] = value = (repository, metadataResource);
        }

        return value;
    }
}