using Confluent.SchemaRegistry;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces
{
    public interface ISchemaRegistryClientFactory
    {
        ISchemaRegistryClient Create(SchemaRegistryConfig config);
    }
}