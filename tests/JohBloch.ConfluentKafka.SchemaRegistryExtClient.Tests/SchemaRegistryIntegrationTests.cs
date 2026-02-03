using System;
using System.Threading.Tasks;
using Confluent.SchemaRegistry;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    using Helpers;

    [LogTestName]
    public class SchemaRegistryIntegrationTests
    {
        // These tests are skipped unless SCHEMA_REGISTRY_URL is provided in the environment
        private static bool HasIntegration => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL"));

        [Fact]
        public async Task Register_Get_Delete_EndToEnd()
        {
            if (!HasIntegration)
            {
                return;
            }

            var url = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL")!;
            var config = new SchemaRegistryConfig { Url = url };
            var services = new ServiceCollection();
            JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.ServiceCollectionExtensions.AddSchemaRegistryExtClient(services, config);
            var sp = services.BuildServiceProvider();

            var client = sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>();

            var subject = $"test-topic-{Guid.NewGuid():N}-value";
            var schema = "{\"type\":\"string\"}";

            var id = await client.RegisterSchemaAsync(subject, schema);
            Assert.True(id > 0);

            var fetched = await client.GetSchemaAsync(subject, 1);
            Assert.Equal(schema, fetched);

            await client.Registrar.DeleteSchemaVersionAsync(subject, 1);

            var afterDelete = await client.GetSchemaAsync(subject, 1);
            Assert.Null(afterDelete);
        }
    }
}