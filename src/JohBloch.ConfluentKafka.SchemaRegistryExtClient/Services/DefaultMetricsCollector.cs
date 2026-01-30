using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    internal class DefaultMetricsCollector : IMetricsCollector
    {
        public void IncrementCacheHit() { }
        public void IncrementCacheMiss() { }
        public void IncrementCacheSet() { }
        public void IncrementTokenRefresh() { }
    }
}