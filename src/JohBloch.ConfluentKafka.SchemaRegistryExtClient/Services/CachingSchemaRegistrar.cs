using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;
using Microsoft.Extensions.Logging;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    internal class CachingSchemaRegistrar : ISchemaRegistrar
    {
        private readonly ISchemaRegistrar _inner;
        private readonly ISchemaCache _cache;
        private readonly ILogger<CachingSchemaRegistrar>? _logger;
        private readonly ConcurrentDictionary<string, Lazy<Task<CachedSchemaInfo?>>> _inflightRegistered = new();
        private readonly ConcurrentDictionary<string, Lazy<Task<Schema?>>> _inflightSchemas = new();

        public CachingSchemaRegistrar(ISchemaRegistrar inner, ISchemaCache cache, ILogger<CachingSchemaRegistrar>? logger = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger;
        }

        public Task<CachedSchemaInfo?> GetRegisteredSchemaAsync(string subject, int version)
        {
            var key = $"{subject}:{version}";

            // Fast-path: cache
            if (_cache.TryGet(key, out var cached))
            {
                _logger?.LogDebug($"Cache hit for subject/version: {key}");
                return Task.FromResult(cached);
            }

            // Deduplicate inflight requests
            var lazy = _inflightRegistered.GetOrAdd(key, k => new Lazy<Task<CachedSchemaInfo?>>(async () =>
            {
                try
                {
                    // Re-check cache in case it was set while the lazy was created
                    if (_cache.TryGet(key, out var fromCache))
                    {
                        return fromCache;
                    }

                    var registered = await _inner.GetRegisteredSchemaAsync(subject, version).ConfigureAwait(false);
                    if (registered == null)
                    {
                        // Cache negative result
                        _cache.Set(key, null);
                        _logger?.LogDebug($"Registered schema not found for subject/version: {key}");
                        return null;
                    }

                    var info = new CachedSchemaInfo
                    {
                        Subject = subject,
                        Id = registered.Id,
                        Schema = registered.Schema,
                        Type = null,
                        SchemaType = registered.SchemaType
                    };
                    _cache.Set(key, info);
                    _cache.Set($"id:{registered.Id}", info);
                    _logger?.LogDebug($"Schema cached for subject/version: {key}");
                    return info;
                }
                finally
                {
                    // Remove inflight marker so future calls can start a new fetch if needed
                    _inflightRegistered.TryRemove(key, out _);
                }
            }, LazyThreadSafetyMode.ExecutionAndPublication));

            return lazy.Value;
        }

        public Task<Schema?> GetSchemaAsync(int id)
        {
            var key = $"id:{id}";

            if (_cache.TryGet(key, out var cached))
            {
                _logger?.LogDebug($"Cache hit for id: {key}");
                if (cached == null) return Task.FromResult<Schema?>(null);
                return Task.FromResult<Schema?>(new Schema(cached.Schema, Enum.TryParse<SchemaType>(cached.SchemaType ?? "AVRO", true, out var st) ? st : SchemaType.Avro));
            }

            var lazy = _inflightSchemas.GetOrAdd(key, k => new Lazy<Task<Schema?>>(async () =>
            {
                try
                {
                    if (_cache.TryGet(key, out var fromCache))
                    {
                        if (fromCache == null) return null;
                        return new Schema(fromCache.Schema, Enum.TryParse<SchemaType>(fromCache.SchemaType ?? "AVRO", true, out var st) ? st : SchemaType.Avro);
                    }

                    var schema = await _inner.GetSchemaAsync(id).ConfigureAwait(false);
                    if (schema == null)
                    {
                        _cache.Set(key, null);
                        _logger?.LogDebug($"Schema not found for id: {key}");
                        return null;
                    }

                    var info = new CachedSchemaInfo
                    {
                        Subject = null,
                        Id = id,
                        Schema = schema.SchemaString,
                        Type = null,
                        SchemaType = schema.SchemaType.ToString()
                    };
                    _cache.Set(key, info);
                    _logger?.LogDebug($"Schema cached for id: {key}");
                    return schema;
                }
                finally
                {
                    _inflightSchemas.TryRemove(key, out _);
                }
            }, LazyThreadSafetyMode.ExecutionAndPublication));

            return lazy.Value;
        }

        public async Task<int> RegisterSchemaAsync(string subject, Schema schema)
        {
            var id = await _inner.RegisterSchemaAsync(subject, schema).ConfigureAwait(false);
            var info = new CachedSchemaInfo
            {
                Subject = subject,
                Id = id,
                Schema = schema.SchemaString,
                Type = null,
                SchemaType = TryGetSchemaTypeString(schema)
            };
            _cache.Set($"{subject}:latest", info);
            _cache.Set($"id:{id}", info);
            // Invalidate any subject-version specific entries for this subject so future reads reflect latest
            var prefix = $"{subject}:";
            foreach (var k in _cache.KeysMatchingPrefix(prefix))
            {
                _cache.Remove(k);
            }
            _logger?.LogDebug($"Schema registered and cached for subject: {subject}, schemaId: {id}");
            return id;
        }

        public async Task DeleteSchemaVersionAsync(string subject, int version)
        {
            await _inner.DeleteSchemaVersionAsync(subject, version).ConfigureAwait(false);
            // Invalidate cache entries related to subject/version
            var key = $"{subject}:{version}";
            _cache.Remove(key);
            _cache.Remove($"{subject}:latest");

            // Also remove any inflight entry
            _inflightRegistered.TryRemove(key, out _);

            // Remove any subject-version specific keys (prefixed)
            var prefix = $"{subject}:";
            foreach (var k in _cache.KeysMatchingPrefix(prefix))
            {
                _cache.Remove(k);
            }

            _logger?.LogDebug($"Cache invalidated for subject: {subject}, version: {version}");
        }

        private static string? TryGetSchemaTypeString(Schema schema)
        {
            var t = schema.GetType();
            var p = t.GetProperty("SchemaType") ?? t.GetProperty("Type");
            var v = p?.GetValue(schema);
            return v?.ToString();
        }
    }
}