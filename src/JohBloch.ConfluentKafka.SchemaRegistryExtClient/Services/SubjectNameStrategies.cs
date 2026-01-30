using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    internal class TopicNameStrategy : ISubjectNameStrategy
    {
        public string GetSubjectName(string topicOrSubject, string? type, string? recordType)
        {
            var lowerType = type?.ToLowerInvariant();
            if (lowerType == "key") return $"{topicOrSubject}-key";
            if (lowerType == "value") return $"{topicOrSubject}-value";
            return topicOrSubject;
        }
    }

    internal class TopicRecordNameStrategy : ISubjectNameStrategy
    {
        public string GetSubjectName(string topicOrSubject, string? type, string? recordType)
        {
            if (!string.IsNullOrWhiteSpace(recordType)) return $"{topicOrSubject}-{recordType}";
            var lowerType = type?.ToLowerInvariant();
            if (lowerType == "key") return $"{topicOrSubject}-key";
            if (lowerType == "value") return $"{topicOrSubject}-value";
            return topicOrSubject;
        }
    }

    internal class RecordNameStrategy : ISubjectNameStrategy
    {
        public string GetSubjectName(string topicOrSubject, string? type, string? recordType)
        {
            if (!string.IsNullOrWhiteSpace(recordType)) return recordType!;
            // Fallback to topic if no recordType specified
            return topicOrSubject;
        }
    }
}
