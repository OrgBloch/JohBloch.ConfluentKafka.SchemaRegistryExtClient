using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;
namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces
{
    /// <summary>
    /// Interface for schema cache implementations.
    /// </summary>
    public interface ISchemaCache : IDisposable
    {
        /// <summary>
        /// Attempts to get a schema from the cache.
        /// </summary>
        bool TryGet(string? key, out CachedSchemaInfo? schema);

        /// <summary>
        /// Adds or updates a schema in the cache.
        /// </summary>
        void Set(string? key, CachedSchemaInfo? schema);

        /// <summary>
        /// Removes a schema from the cache.
        /// </summary>
        void Remove(string? key);

        /// <summary>
        /// Clears all cached schemas.
        /// </summary>
        void Clear();

        /// <summary>
        /// Helper to query keys with a prefix. Implementations should be efficient for in-memory caches.
        /// </summary>
        IEnumerable<string> KeysMatchingPrefix(string prefix);
        /// <summary>
        /// Number of cache hits.
        /// </summary>
        int HitCount { get; }

        /// <summary>
        /// Number of cache misses.
        /// </summary>
        int MissCount { get; }

        /// <summary>
        /// Current number of items in the cache.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Raised when a cache hit occurs.
        /// </summary>
        event EventHandler<string?>? CacheHit;

        /// <summary>
        /// Raised when a cache miss occurs.
        /// </summary>
        event EventHandler<string?>? CacheMiss;
    }
}
