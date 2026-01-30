using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.SchemaRegistryExtClient.Services
{
    public class SchemaRegistryExtClient : ISchemaRegistryExtClient
    {
        private readonly ITokenManager? _tokenManager;
        private readonly ISchemaCache _cache;
        private readonly bool _useTokenManager;
        private SchemaRegistryConfig _config;
        private string? _lastToken;
        private ISchemaRegistryClient? _currentClient;
        private readonly ISchemaRegistryClientFactory _clientFactory;
        public ISchemaCache Cache => _cache;
        private readonly ILogger<SchemaRegistryExtClient>? _logger;
        private readonly SchemaClientOptions _options;
        private readonly ISubjectNameStrategy? _subjectNameStrategy;

        // Cached registrar instance. When the underlying registry client is recreated
        // (e.g., after token refresh), this will be refreshed to wrap the new client.
        private ISchemaRegistrar? _registrar;

        // Create a registrar that wraps a concrete client instance.
        private ISchemaRegistrar CreateRegistrar(ISchemaRegistryClient client)
        {
            return new CachingSchemaRegistrar(new DefaultSchemaRegistrar(client), _cache, _logger as ILogger<CachingSchemaRegistrar>);
        }

        /// <summary>
        /// Public access to the registrar used by the client. This ensures a single
        /// registrar instance is reused across operations and can be registered in DI.
        /// </summary>
        public ISchemaRegistrar Registrar => _registrar ??= (_currentClient != null ? CreateRegistrar(_currentClient) : CreateRegistrar(new CachedSchemaRegistryClient(_config)));

        public SchemaRegistryExtClient(
            SchemaRegistryConfig config,
            Func<Task<(string token, DateTime expiresAt)>>? tokenRefreshFunc = null,
            ISchemaCache? cache = null,
            SchemaClientOptions? options = null,
            ISchemaRegistryClientFactory? clientFactory = null)
        {
            _options = options ?? new SchemaClientOptions();
            _logger = _options.Logger as ILogger<SchemaRegistryExtClient>;
            _useTokenManager = tokenRefreshFunc != null;
            _tokenManager = _useTokenManager ? new TokenManager(tokenRefreshFunc!) : null; // assigned to ITokenManager
            _cache = cache ?? new InMemorySchemaCache(_options.CacheOptions, _logger as ILogger<InMemorySchemaCache>);
            _config = config;
            _clientFactory = clientFactory ?? new DefaultSchemaRegistryClientFactory();
            // Resolve subject naming strategy implementation (explicit impl takes precedence)
            _subjectNameStrategy = _options.SubjectNameStrategyImplementation ?? (_options.SubjectNameStrategy.HasValue ? _options.SubjectNameStrategy.Value switch
            {
                JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.TopicName => new TopicNameStrategy(),
                JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.TopicRecordName => new TopicRecordNameStrategy(),
                JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.RecordName => new RecordNameStrategy(),
                _ => null
            } : null);

            // Initialization of _currentClient is deferred to GetClientAsync to avoid sync-over-async
            if (!_useTokenManager)
            {
                _currentClient = new CachedSchemaRegistryClient(_config);
            }
            _logger?.LogInformation("SchemaClient initialized");
        }

        private readonly SemaphoreSlim _clientLock = new(1,1);

        public async Task<ISchemaRegistryClient> GetClientAsync()
        {
            // Fast path without token manager
            if (!_useTokenManager)
            {
                if (_currentClient == null)
                {
                    _currentClient = _clientFactory.Create(_config);
                    // Ensure registrar uses this concrete client
                    _registrar = CreateRegistrar(_currentClient);
                }
                return _currentClient!;
            }

            var token = await _tokenManager!.GetTokenAsync();
            if (_currentClient == null || token != _lastToken)
            {
                // Ensure only one thread recreates the client
                await _clientLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    // re-check inside lock
                    if (_currentClient == null || token != _lastToken)
                    {
                        _lastToken = token;
                        _config.Set("bearer.auth.token", token);

                        // Dispose the old client if it exists to prevent resource leaks
                        _currentClient?.Dispose();
                        _currentClient = _clientFactory.Create(_config);
                        // Refresh registrar so it wraps the new client instance
                        _registrar = CreateRegistrar(_currentClient);
                    }
                }
                finally
                {
                    _clientLock.Release();
                }
            }
            return _currentClient!;
        }

        public async Task<string?> GetSchemaAsync(string subject, int version)
        {
            var key = $"{subject}:{version}";
            if (_cache.TryGet(key, out var cachedSchema) && cachedSchema != null)
            {
                _logger?.LogDebug($"Cache hit for subject/version: {key}");
                return cachedSchema.Schema;
            }

            var client = await GetClientAsync();
            try
            {
                var registered = await client.GetRegisteredSchemaAsync(subject, version);
                var schema = registered.SchemaString;
                var info = new CachedSchemaInfo
                {
                    Subject = subject,
                    Id = registered.Id,
                    Schema = schema,
                    Type = null,
                    SchemaType = null
                };
                _cache.Set(key, info);
                _logger?.LogDebug($"Schema cached for subject/version: {key}");
                return schema;
            }
            catch (Exception ex) when (ex is Confluent.SchemaRegistry.SchemaRegistryException || ex is KeyNotFoundException)
            {
                _logger?.LogWarning(ex, $"Schema not found for subject/version: {key}");
                return null;
            }
        }

        /// <summary>
        /// Helper to parse the schema id from the Confluent wire format (first 5 bytes).
        /// </summary>
        private static int ParseSchemaId(byte[] message)
        {
            if (message == null || message.Length < 5)
                throw new ArgumentException("Message must be at least 5 bytes.");
            return (message[1] << 24) | (message[2] << 16) | (message[3] << 8) | message[4];
        }

        /// <summary>
        /// Retrieves a schema based on the first 5 bytes of a message (Confluent wire format).
        /// If present in cache it is returned; otherwise the token (if any) is refreshed and the schema is fetched from the registry.
        /// Returns null when no schema is found.
        /// </summary>
        public async Task<string?> GetSchema(byte[] message)
        {
            var schemaId = ParseSchemaId(message);
            var key = $"id:{schemaId}";
            if (_cache.TryGet(key, out var cachedSchema) && cachedSchema != null)
            {
                _logger?.LogDebug($"Cache hit for id: {key}");
                return cachedSchema.Schema;
            }

            var client = await GetClientAsync();
            _registrar ??= CreateRegistrar(client);
            try
            {
                var schemaObj = await _registrar.GetSchemaAsync(schemaId);
                if (schemaObj == null)
                {
                    return null;
                }
                return schemaObj.SchemaString;
            }
            catch (Exception ex) when (ex is Confluent.SchemaRegistry.SchemaRegistryException || ex is KeyNotFoundException)
            {
                _logger?.LogWarning(ex, $"Schema not found for id: {schemaId}");
                return null;
            }
        }


        /// <summary>
        /// Registers a new schema in the Schema Registry and returns the assigned schema id. The schema is also cached.
        /// </summary>
        public async Task<int> RegisterSchemaAsync(
            string topicOrSubject,
            string schema,
            string schemaType = "AVRO",
            string? type = null,
            string? recordType = null)
        {
            var client = await GetClientAsync();
            _registrar ??= CreateRegistrar(client);
            var subject = GetSubjectName(topicOrSubject, type, recordType);

            var typeEnum = ParseSchemaType(schemaType);

            var schemaId = await _registrar.RegisterSchemaAsync(subject, new Schema(schema, typeEnum));
            _logger?.LogInformation($"Schema registered and cached for subject: {subject}, schemaId: {schemaId}");
            return schemaId;
        }

        /// <summary>
        /// Parses a schema type string (e.g. "AVRO", "PROTOBUF", "JSON") into the Confluent SchemaType enum.
        /// Defaults to AVRO when parsing fails or when the value is null/empty.
        /// </summary>
        internal SchemaType ParseSchemaType(string? schemaType)
        {
            if (Enum.TryParse<SchemaType>(schemaType ?? string.Empty, true, out var parsed))
            {
                return parsed;
            }
            return SchemaType.Avro;
        }

        /// <summary>
        /// Registrerer et nyt value-skema for et topic og returnerer schemaId.
        /// </summary>
        public Task<int> RegisterValueSchemaAsync(string topic, string schema, string schemaType = "AVRO", string? recordType = null)
        {
            return RegisterSchemaAsync(topic, schema, schemaType, "value", recordType);
        }

        /// <summary>
        /// Registers a key schema for a topic and returns the schema id.
        /// </summary>
        public Task<int> RegisterKeySchemaAsync(string topic, string schema, string schemaType = "AVRO", string? recordType = null)
        {
            return RegisterSchemaAsync(topic, schema, schemaType, "key", recordType);
        }

        // Internal helper extracted to allow testing of subject name behavior. Matches the logic used when registering schemas.
        internal string GetSubjectName(string topicOrSubject, string? type, string? recordType)
        {
            // If a concrete strategy implementation is provided, use it (supports DI/customization)
            if (_subjectNameStrategy != null)
            {
                return _subjectNameStrategy.GetSubjectName(topicOrSubject, type, recordType);
            }

            var strategy = _options?.SubjectNameStrategy;
            var lowerType = type?.ToLowerInvariant();
            if (strategy.HasValue)
            {
                switch (strategy.Value)
                {
                    case JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.RecordName:
                        if (!string.IsNullOrWhiteSpace(recordType)) return recordType!;
                        break;
                    case JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.TopicRecordName:
                        if (!string.IsNullOrWhiteSpace(recordType)) return $"{topicOrSubject}-{recordType}";
                        break;
                    case JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.TopicName:
                        if (lowerType == "key") return $"{topicOrSubject}-key";
                        if (lowerType == "value") return $"{topicOrSubject}-value";
                        return topicOrSubject;
                }
            }

            // Legacy behavior: prefer topic-record when recordType present, otherwise topic-key/value
            if (lowerType == "key") return !string.IsNullOrWhiteSpace(recordType) ? $"{topicOrSubject}-{recordType}" : $"{topicOrSubject}-key";
            if (lowerType == "value") return !string.IsNullOrWhiteSpace(recordType) ? $"{topicOrSubject}-{recordType}" : $"{topicOrSubject}-value";
            return topicOrSubject;
        }








        public void Dispose()
        {
            _currentClient?.Dispose();
            _clientLock?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_currentClient != null)
            {
                await Task.Run(() => _currentClient.Dispose()).ConfigureAwait(false);
            }
            _clientLock?.Dispose();
        }

        /// <summary>
        /// Exposes the token manager when one was provided via constructor/DI.
        /// </summary>
        public ITokenManager? TokenManager => _tokenManager;
    }
}