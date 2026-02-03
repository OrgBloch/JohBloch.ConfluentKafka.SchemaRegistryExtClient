using Confluent.SchemaRegistry;
using Xunit;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    using Helpers;

    [LogTestName]
    public class SchemaTypeParsingTests
    {
        [Theory]
        [InlineData("AVRO", SchemaType.Avro)]
        [InlineData("avro", SchemaType.Avro)]
        [InlineData("PROTOBUF", SchemaType.Protobuf)]
        [InlineData("protobuf", SchemaType.Protobuf)]
        [InlineData("JSON", SchemaType.Json)]
        [InlineData(null, SchemaType.Avro)]
        [InlineData("", SchemaType.Avro)]
        [InlineData("INVALID", SchemaType.Avro)]
        public void ParseSchemaType_HandlesKnownValues(string? input, SchemaType expected)
        {
            var client = new JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient(
                new SchemaRegistryConfig { Url = "http://localhost" },
                tokenManager: null,
                cache: null,
                options: null);
            var parsed = client.ParseSchemaType(input);
            Assert.Equal(expected, parsed);
        }
    }
}