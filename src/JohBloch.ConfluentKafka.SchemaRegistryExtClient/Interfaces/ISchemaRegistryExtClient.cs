using Confluent.SchemaRegistry;
namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces
{
    /// <summary>
    /// Extended interface for Schema Registry client with additional caching and token management features.
    /// </summary>
    public interface ISchemaRegistryExtClient : IDisposable
    {
        /// <summary>
        /// Gets the underlying ISchemaRegistryClient, refreshing the token if necessary.
        /// </summary>
        Task<ISchemaRegistryClient> GetClientAsync();

        /// <summary>
        /// Gets a schema by subject and version, using cache if available.
        /// </summary>
        Task<string?> GetSchemaAsync(string subject, int version);


        /// <summary>
        /// Gets a schema from a wire-format message (first 5 bytes = schemaId), using cache if available.
        /// </summary>
        Task<string?> GetSchema(byte[] message);

        /// <summary>
        /// Registers a schema in Schema Registry with flexible subject naming.
        /// </summary>
        Task<int> RegisterSchemaAsync(
            string topicOrSubject,
            string schema,
            string schemaType = "AVRO",
            string? type = null,
            string? recordType = null);

        /// <summary>
        /// Registers a value schema for a topic.
        /// </summary>
        Task<int> RegisterValueSchemaAsync(string topic, string schema, string schemaType = "AVRO", string? recordType = null);

        /// <summary>
        /// Registers a key schema for a topic.
        /// </summary>
        Task<int> RegisterKeySchemaAsync(string topic, string schema, string schemaType = "AVRO", string? recordType = null);
    }
}