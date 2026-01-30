using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using Moq;
using Xunit;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    public class CachingSchemaRegistrarTests
    {
        [Fact]
        public async Task GetRegisteredSchemaAsync_CachesResult()
        {
            var mock = new Mock<ISchemaRegistrar>();
            mock.Setup(m => m.GetRegisteredSchemaAsync("topic", 1))
                .ReturnsAsync(new CachedSchemaInfo { Id = 123, Schema = "schema", SchemaType = "AVRO" })
                .Verifiable();

            var metrics = new Moq.Mock<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces.IMetricsCollector>();
            var cache = new InMemorySchemaCache(null, null, metrics.Object);
            var registrar = new CachingSchemaRegistrar(mock.Object, cache);

            var first = await registrar.GetRegisteredSchemaAsync("topic", 1);
            var second = await registrar.GetRegisteredSchemaAsync("topic", 1);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Id, second!.Id);
            mock.Verify(m => m.GetRegisteredSchemaAsync("topic", 1), Times.Once);
            metrics.Verify(m => m.IncrementCacheSet(), Moq.Times.Once);
            metrics.Verify(m => m.IncrementCacheHit(), Moq.Times.Once);
        }

        [Fact]
        public async Task RegisterSchemaAsync_UpdatesCache()
        {
            var mock = new Mock<ISchemaRegistrar>();
            mock.Setup(m => m.RegisterSchemaAsync("topic", It.IsAny<Schema>()))
                .ReturnsAsync(321)
                .Verifiable();

            var cache = new InMemorySchemaCache();
            var registrar = new CachingSchemaRegistrar(mock.Object, cache);

            var id = await registrar.RegisterSchemaAsync("topic", new Schema("s", SchemaType.Avro));
            Assert.Equal(321, id);

            // Ensure cached by id
            Assert.True(cache.TryGet("id:321", out var cached));
            Assert.Equal(321, cached?.Id);
            mock.Verify(m => m.RegisterSchemaAsync("topic", It.IsAny<Schema>()), Times.Once);
        }

        [Fact]
        public async Task DeleteSchemaVersionAsync_InvalidatesCache()
        {
            var mock = new Mock<ISchemaRegistrar>();
            mock.SetupSequence(m => m.GetRegisteredSchemaAsync("topic", 1))
                .ReturnsAsync(new CachedSchemaInfo { Id = 1, Schema = "first", SchemaType = "AVRO" })
                .ReturnsAsync(new CachedSchemaInfo { Id = 1, Schema = "second", SchemaType = "AVRO" });

            mock.Setup(m => m.DeleteSchemaVersionAsync("topic", 1)).Returns(Task.CompletedTask).Verifiable();

            var cache = new InMemorySchemaCache();
            var registrar = new CachingSchemaRegistrar(mock.Object, cache);

            var first = await registrar.GetRegisteredSchemaAsync("topic", 1);
            Assert.Equal("first", first?.Schema);

            // Second call should be served from cache
            var second = await registrar.GetRegisteredSchemaAsync("topic", 1);
            Assert.Equal("first", second?.Schema);
            mock.Verify(m => m.GetRegisteredSchemaAsync("topic", 1), Times.Once);

            // Delete and ensure cache invalidation
            await registrar.DeleteSchemaVersionAsync("topic", 1);
            mock.Verify(m => m.DeleteSchemaVersionAsync("topic", 1), Times.Once);

            // Next call should fetch again from inner and yield updated schema
            var third = await registrar.GetRegisteredSchemaAsync("topic", 1);
            Assert.Equal("second", third?.Schema);
            mock.Verify(m => m.GetRegisteredSchemaAsync("topic", 1), Times.Exactly(2));
        }

        [Fact]
        public async Task GetRegisteredSchemaAsync_NegativeCaching()
        {
            var mock = new Mock<ISchemaRegistrar>();
            mock.SetupSequence(m => m.GetRegisteredSchemaAsync("topic", 42))
                .ReturnsAsync((CachedSchemaInfo?)null)
                .ReturnsAsync(new CachedSchemaInfo { Id = 9, Schema = "later", SchemaType = "AVRO" });

            var cache = new InMemorySchemaCache(new Models.SchemaCacheOptions { TimeToLiveSeconds = 60 });
            var registrar = new CachingSchemaRegistrar(mock.Object, cache);

            var first = await registrar.GetRegisteredSchemaAsync("topic", 42);
            Assert.Null(first);

            // Second call should be served from negative cache (no inner call)
            var second = await registrar.GetRegisteredSchemaAsync("topic", 42);
            Assert.Null(second);
            mock.Verify(m => m.GetRegisteredSchemaAsync("topic", 42), Times.Once);

            // After delete/invalidation, next call should fetch again
            await registrar.DeleteSchemaVersionAsync("topic", 42);
            var third = await registrar.GetRegisteredSchemaAsync("topic", 42);
            Assert.Equal("later", third?.Schema);
            mock.Verify(m => m.GetRegisteredSchemaAsync("topic", 42), Times.Exactly(2));
        }
    }
}