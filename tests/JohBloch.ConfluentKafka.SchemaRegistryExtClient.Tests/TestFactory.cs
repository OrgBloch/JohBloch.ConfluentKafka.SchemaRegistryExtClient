using System;
using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    internal class TestFactory : ISchemaRegistryClientFactory
    {
        private readonly Func<ISchemaRegistryClient> _create;
        public TestFactory(Func<ISchemaRegistryClient> create)
        {
            _create = create;
        }

        public ISchemaRegistryClient Create(SchemaRegistryConfig config) => _create();
    }
}