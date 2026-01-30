namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces
{
    /// <summary>
    /// Async interface for schema cache implementations (for external caches).
    /// </summary>
    public interface ISchemaCacheAsync : IDisposable
    {
        Task<bool> TryGetAsync(string? key, out CachedSchemaInfo? schema);
        Task SetAsync(string? key, CachedSchemaInfo? schema);
        Task RemoveAsync(string? key);
        Task ClearAsync();
        SchemaCacheOptions Options { get; }
        int HitCount { get; }
        int MissCount { get; }
        int Count { get; }
        event EventHandler<string?>? CacheHit;
        event EventHandler<string?>? CacheMiss;
    }
}
