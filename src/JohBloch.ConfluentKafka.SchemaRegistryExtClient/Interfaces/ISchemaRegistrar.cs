using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces
{
    public interface ISchemaRegistrar
    {
        // Return null when not found
        Task<CachedSchemaInfo?> GetRegisteredSchemaAsync(string subject, int version);
        Task<Schema?> GetSchemaAsync(int id);
        Task<int> RegisterSchemaAsync(string subject, Schema schema);
        Task DeleteSchemaVersionAsync(string subject, int version);
    }
}