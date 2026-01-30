namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models
{
    public class SchemaCacheOptions
    {
        public int MaxSize { get; set; } = 1000;
        public int TimeToLiveSeconds { get; set; } = 1800;
    }
}
