namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models
{
    /// <summary>
    /// Strategy for constructing subject names for a topic or record type.
    /// </summary>
    public interface ISubjectNameStrategy
    {
        string GetSubjectName(string topicOrSubject, string? type, string? recordType);
    }
}