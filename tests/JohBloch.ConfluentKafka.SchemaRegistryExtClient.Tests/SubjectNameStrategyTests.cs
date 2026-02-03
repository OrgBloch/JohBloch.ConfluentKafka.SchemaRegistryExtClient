using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;
using Xunit;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    using Helpers;

    [LogTestName]
    public class SubjectNameStrategyTests
    {
        [Theory]
        [InlineData(SubjectNameStrategy.RecordName, "my-topic", "key", "MyRecord", "MyRecord")]
        [InlineData(SubjectNameStrategy.RecordName, "my-topic", "value", "MyRecord", "MyRecord")]

        [InlineData(SubjectNameStrategy.TopicRecordName, "my-topic", "key", "MyRecord", "my-topic-MyRecord")]
        [InlineData(SubjectNameStrategy.TopicRecordName, "my-topic", "value", "MyRecord", "my-topic-MyRecord")]

        [InlineData(SubjectNameStrategy.TopicName, "my-topic", "key", null, "my-topic-key")]
        [InlineData(SubjectNameStrategy.TopicName, "my-topic", "value", null, "my-topic-value")]

        // Legacy behavior when strategy is null: prefer topic-record when recordType present
        [InlineData(null, "my-topic", "key", "MyRecord", "my-topic-MyRecord")]
        [InlineData(null, "my-topic", "key", null, "my-topic-key")]
        [InlineData(null, "my-topic", "value", null, "my-topic-value")]
        public void GetSubjectName_HonorsStrategy(SubjectNameStrategy? strategy, string topic, string type, string? recordType, string expected)
        {
            var config = new Confluent.SchemaRegistry.SchemaRegistryConfig { Url = "http://localhost" };
            var options = new Models.SchemaClientOptions { SubjectNameStrategy = strategy };
            var client = new JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient(
                config,
                tokenManager: null,
                cache: null,
                options: options);

            var result = client.GetSubjectName(topic, type, recordType);

            Assert.Equal(expected, result);
        }
    }
}