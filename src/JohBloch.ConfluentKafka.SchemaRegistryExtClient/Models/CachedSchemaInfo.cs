namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models
{
    public class CachedSchemaInfo
    {
        public string? Subject { get; set; }
        public int? Id { get; set; }
        public string? Schema { get; set; }
        public string? Type { get; set; } // "key" eller "value"
        public string? SchemaType { get; set; } // fx "AVRO"
    }
}
