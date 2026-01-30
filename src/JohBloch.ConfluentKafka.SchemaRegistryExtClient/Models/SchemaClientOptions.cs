using Microsoft.Extensions.Logging;
namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models
{
    public class SchemaClientOptions
    {
        public SchemaCacheOptions? CacheOptions { get; set; } = new SchemaCacheOptions();
        public ILogger? Logger { get; set; }
        /// <summary>
        /// Optional subject name strategy that aligns with serializer configuration
        /// (equivalent to serializer config 'subject.name.strategy'). If null, legacy behavior is used
        /// (topic-record when recordType present, otherwise topic-key/value).
        /// </summary>
        public SubjectNameStrategy? SubjectNameStrategy { get; set; }

        /// <summary>
        /// An optional concrete strategy implementation. If provided, this takes precedence over the enum value.
        /// </summary>
        public ISubjectNameStrategy? SubjectNameStrategyImplementation { get; set; }

        // Extend with more options as needed
    }
}
