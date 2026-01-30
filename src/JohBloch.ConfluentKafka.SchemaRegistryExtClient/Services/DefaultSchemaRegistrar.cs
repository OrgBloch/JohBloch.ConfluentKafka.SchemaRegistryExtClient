using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    internal class DefaultSchemaRegistrar : ISchemaRegistrar
    {
        private readonly ISchemaRegistryClient _inner;

        public DefaultSchemaRegistrar(ISchemaRegistryClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<CachedSchemaInfo?> GetRegisteredSchemaAsync(string subject, int version)
        {
            try
            {
                var r = await _inner.GetRegisteredSchemaAsync(subject, version).ConfigureAwait(false);
                return new CachedSchemaInfo
                {
                    Subject = subject,
                    Id = r.Id,
                    Schema = r.SchemaString,
                    Type = null,
                    SchemaType = r.SchemaType.ToString()
                };
            }
            catch (Exception ex) when (ex is Confluent.SchemaRegistry.SchemaRegistryException || ex is KeyNotFoundException)
            {
                // Not found
                return null;
            }
        }

        public async Task<Schema?> GetSchemaAsync(int id)
        {
            try
            {
                return await _inner.GetSchemaAsync(id).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is Confluent.SchemaRegistry.SchemaRegistryException || ex is KeyNotFoundException)
            {
                return null;
            }
        }

        public Task<int> RegisterSchemaAsync(string subject, Schema schema)
        {
            return _inner.RegisterSchemaAsync(subject, schema);
        }

        public Task DeleteSchemaVersionAsync(string subject, int version)
        {
            var t = _inner.GetType();
            var m = t.GetMethod("DeleteSchemaVersionAsync", new[] { typeof(string), typeof(int) });
            if (m != null)
            {
                var task = (Task)m.Invoke(_inner, new object[] { subject, version })!;
                return task;
            }
            m = t.GetMethod("DeleteSubjectAsync", new[] { typeof(string) });
            if (m != null)
            {
                var task = (Task)m.Invoke(_inner, new object[] { subject })!;
                return task;
            }
            return Task.CompletedTask;
        }
    }
}