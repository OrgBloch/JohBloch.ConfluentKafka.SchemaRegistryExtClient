using System.Threading.Tasks;
using Xunit;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    public class OpenTelemetryMetricsCollectorTests
    {
        [Fact]
        public Task Counters_Increment()
        {
            var collector = new OpenTelemetryMetricsCollector();

            collector.IncrementCacheHit();
            collector.IncrementCacheMiss();
            collector.IncrementCacheSet();
            collector.IncrementTokenRefresh();

            Assert.Equal(1, collector.HitCount);
            Assert.Equal(1, collector.MissCount);
            Assert.Equal(1, collector.SetCount);
            Assert.Equal(1, collector.TokenRefreshCount);

            return Task.CompletedTask;
        }
    }
}