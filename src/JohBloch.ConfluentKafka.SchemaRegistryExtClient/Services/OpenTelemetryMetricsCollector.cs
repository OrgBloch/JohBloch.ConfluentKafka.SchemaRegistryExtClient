using System.Diagnostics.Metrics;
using System.Threading;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    internal class OpenTelemetryMetricsCollector : IMetricsCollector
    {
        private static readonly Meter _meter = new("JohBloch.SchemaRegistryExtClient", "1.0.0");
        private readonly Counter<long> _cacheHit;
        private readonly Counter<long> _cacheMiss;
        private readonly Counter<long> _cacheSet;
        private readonly Counter<long> _tokenRefresh;

        // local counters for easy testing/inspection
        private long _hitCount;
        private long _missCount;
        private long _setCount;
        private long _tokenRefreshCount;

        public OpenTelemetryMetricsCollector()
        {
            _cacheHit = _meter.CreateCounter<long>("schema_registry.cache.hit", description: "Number of cache hits");
            _cacheMiss = _meter.CreateCounter<long>("schema_registry.cache.miss", description: "Number of cache misses");
            _cacheSet = _meter.CreateCounter<long>("schema_registry.cache.set", description: "Number of cache sets");
            _tokenRefresh = _meter.CreateCounter<long>("schema_registry.token.refresh", description: "Number of token refreshes");
        }

        public void IncrementCacheHit()
        {
            _cacheHit.Add(1);
            Interlocked.Increment(ref _hitCount);
        }

        public void IncrementCacheMiss()
        {
            _cacheMiss.Add(1);
            Interlocked.Increment(ref _missCount);
        }

        public void IncrementCacheSet()
        {
            _cacheSet.Add(1);
            Interlocked.Increment(ref _setCount);
        }

        public void IncrementTokenRefresh()
        {
            _tokenRefresh.Add(1);
            Interlocked.Increment(ref _tokenRefreshCount);
        }

        // Properties useful for unit tests
        public long HitCount => Interlocked.Read(ref _hitCount);
        public long MissCount => Interlocked.Read(ref _missCount);
        public long SetCount => Interlocked.Read(ref _setCount);
        public long TokenRefreshCount => Interlocked.Read(ref _tokenRefreshCount);
    }
}