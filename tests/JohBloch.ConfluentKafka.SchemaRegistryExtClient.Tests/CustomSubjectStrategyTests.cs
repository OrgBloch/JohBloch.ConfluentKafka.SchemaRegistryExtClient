using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;
using Xunit;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    using Helpers;

    [LogTestName]
    public class CustomSubjectStrategyTests
    {
        private class FixedStrategy : ISubjectNameStrategy
        {
            private readonly string _fixed;
            public FixedStrategy(string fixedName) => _fixed = fixedName;
            public string GetSubjectName(string topicOrSubject, string? type, string? recordType) => _fixed;
        }

        [Fact]
        public void CustomStrategy_IsUsedWhenProvided()
        {
            var options = new SchemaClientOptions { SubjectNameStrategyImplementation = new FixedStrategy("my-fixed-subject") };
            var client = new JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient(
                new Confluent.SchemaRegistry.SchemaRegistryConfig { Url = "http://localhost" },
                tokenManager: null,
                cache: null,
                options: options);

            var subject = client.GetSubjectName("orders", "value", "OrderValue");

            Assert.Equal("my-fixed-subject", subject);
        }
    }
}
