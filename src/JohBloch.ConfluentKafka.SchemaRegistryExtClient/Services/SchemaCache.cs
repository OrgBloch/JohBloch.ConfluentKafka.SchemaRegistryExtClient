using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;
namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    public class InMemorySchemaCache : ISchemaCache
    {
        // Allow storing a null CachedSchemaInfo entry to represent a negative cache (not found)
        private readonly ConcurrentDictionary<string, (CachedSchemaInfo? info, DateTime cachedAt, int hits)> _cache = new();
        private int _hitCount;
        private int _missCount;
        private bool _disposed;
        private readonly ILogger<InMemorySchemaCache>? _logger;
        private readonly Interfaces.IMetricsCollector _metrics;
        public SchemaCacheOptions Options { get; }

        public event EventHandler<string?>? CacheHit;
        public event EventHandler<string?>? CacheMiss;

        public InMemorySchemaCache(SchemaCacheOptions? options = null, ILogger<InMemorySchemaCache>? logger = null, Interfaces.IMetricsCollector? metrics = null)
        {
            Options = options ?? new SchemaCacheOptions();
            _logger = logger;
            _metrics = metrics ?? new DefaultMetricsCollector();
        }

        public int HitCount => _hitCount;
        public int MissCount => _missCount;
        public int Count => _cache.Count;

        public bool TryGet(string? key, out CachedSchemaInfo? schema)
        {
            schema = null;
            if (string.IsNullOrEmpty(key))
            {
                System.Threading.Interlocked.Increment(ref _missCount);
                CacheMiss?.Invoke(this, key);
                _logger?.LogInformation($"Cache miss for key: {key}");
                return false;
            }

            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow - entry.cachedAt < TimeSpan.FromSeconds(Options.TimeToLiveSeconds))
                {
                    schema = entry.info; // may be null: negative cache
                    _cache[key] = (entry.info, entry.cachedAt, entry.hits + 1);
                    System.Threading.Interlocked.Increment(ref _hitCount);
                    CacheHit?.Invoke(this, key);
                    _metrics.IncrementCacheHit();
                    _logger?.LogInformation($"Cache hit for key: {key}");
                    return true;
                }
                _cache.TryRemove(key, out _);
                _logger?.LogInformation($"Cache expired for key: {key}");
            }

            System.Threading.Interlocked.Increment(ref _missCount);
            CacheMiss?.Invoke(this, key);
            _metrics.IncrementCacheMiss();
            _logger?.LogInformation($"Cache miss for key: {key}");
            return false;
        }

        // Allows setting a null schema to indicate a negative cache (not found). Use TTL to control duration.
        public void Set(string? key, CachedSchemaInfo? schema)
        {
            if (string.IsNullOrEmpty(key))
            {
                _logger?.LogWarning("Attempt to set cache with null key");
                return;
            }

            if (_cache.Count >= Options.MaxSize)
            {
                var toRemove = _cache.OrderBy(x => x.Value.hits).ThenBy(x => x.Value.cachedAt).FirstOrDefault();
                if (!string.IsNullOrEmpty(toRemove.Key))
                {
                    _cache.TryRemove(toRemove.Key, out _);
                    _logger?.LogInformation($"Cache evicted key: {toRemove.Key}");
                }
            }
            _cache[key] = (schema, DateTime.UtcNow, 1);
            _metrics.IncrementCacheSet();
            _logger?.LogInformation($"Cache set for key: {key}");
        }

        public void Remove(string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _cache.TryRemove(key, out _);
            _logger?.LogInformation($"Cache removed key: {key}");
        }

        public IEnumerable<string> KeysMatchingPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return Enumerable.Empty<string>();
            return _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal));
        }

        public void Clear()
        {
            _cache.Clear();
            _logger?.LogInformation("Cache cleared");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cache.Clear();
                _disposed = true;
                _logger?.LogInformation("Cache disposed");
            }
        }
    }
}
