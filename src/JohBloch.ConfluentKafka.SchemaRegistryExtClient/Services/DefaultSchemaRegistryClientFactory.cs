using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    internal class DefaultSchemaRegistryClientFactory : ISchemaRegistryClientFactory
    {
        public ISchemaRegistryClient Create(SchemaRegistryConfig config)
        {
            return new CachedSchemaRegistryClient(config);
        }
    }
}