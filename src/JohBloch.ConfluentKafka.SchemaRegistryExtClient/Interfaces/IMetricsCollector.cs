namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces
{
    public interface IMetricsCollector
    {
        void IncrementCacheHit();
        void IncrementCacheMiss();
        void IncrementCacheSet();
        void IncrementTokenRefresh();
    }
}