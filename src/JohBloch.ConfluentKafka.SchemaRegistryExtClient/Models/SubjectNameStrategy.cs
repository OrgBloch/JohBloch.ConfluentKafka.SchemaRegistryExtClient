namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models
{
    /// <summary>
    /// Subject naming strategies that correspond to the serializer configuration
    /// (for example, the serializer setting "subject.name.strategy").
    /// </summary>
    public enum SubjectNameStrategy
    {
        /// <summary>
        /// TopicNameStrategy (subject = topic-key or topic-value)
        /// </summary>
        TopicName,

        /// <summary>
        /// TopicRecordNameStrategy (subject = topic-recordType if record type provided)
        /// </summary>
        TopicRecordName,

        /// <summary>
        /// RecordNameStrategy (subject = recordType)
        /// </summary>
        RecordName
    }
}