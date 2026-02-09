namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;

using ModelSubjectNameStrategy = JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy;

public sealed class SchemaRegistryExtClient : ISchemaRegistryExtClient
{
    private static readonly ISubjectNameStrategy TopicNameStrategy = new TopicNameStrategy();
    private static readonly ISubjectNameStrategy TopicRecordNameStrategy = new TopicRecordNameStrategy();
    private static readonly ISubjectNameStrategy RecordNameStrategy = new RecordNameStrategy();

    private readonly SchemaRegistryConfig _config;
    private readonly ISchemaCache? _cache;
    private readonly SchemaClientOptions _options;
    private readonly ISchemaRegistryClientFactory _clientFactory;

    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private ISchemaRegistryClient? _client;
    private string? _clientToken;
    private bool _disposed;

    private readonly RegistrarProxy _registrarProxy;
    private ISchemaRegistrar? _uncachedRegistrar;

    public ISchemaRegistrar Registrar => _registrarProxy;

    public ITokenManager? TokenManager { get; }

    public SchemaRegistryExtClient(
        SchemaRegistryConfig config,
        ITokenManager? tokenManager,
        ISchemaCache? cache,
        SchemaClientOptions? options = null,
        ISchemaRegistryClientFactory? clientFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        TokenManager = tokenManager;
        _cache = cache;
        _options = options ?? new SchemaClientOptions();
        _clientFactory = clientFactory ?? new DefaultSchemaRegistryClientFactory();

        _registrarProxy = new RegistrarProxy();

        ApplyConfluentCloudOAuthExtensions();
    }

    public SchemaRegistryExtClient(
        SchemaRegistryConfig config,
        Func<Task<(string token, DateTime expiresAt)>> tokenRefreshFunc,
        ISchemaCache? cache,
        SchemaClientOptions? options = null,
        ISchemaRegistryClientFactory? clientFactory = null)
        : this(
            config,
            tokenManager: new TokenManager(tokenRefreshFunc ?? throw new ArgumentNullException(nameof(tokenRefreshFunc))),
            cache,
            options,
            clientFactory)
    {
    }

    public async Task<ISchemaRegistryClient> GetClientAsync()
    {
        ThrowIfDisposed();

        // Fetch token outside the lock to avoid blocking other callers while a refresh/network call is in progress.
        var token = TokenManager != null
            ? await TokenManager.GetTokenAsync().ConfigureAwait(false)
            : null;

        // If OAuth/token refresh is configured, we must not create a new client with an empty token.
        // If a client already exists with a previous token, keep using it (transient refresh failure).
        if (TokenManager != null && string.IsNullOrWhiteSpace(token))
        {
            var existingClient = _client;
            if (existingClient != null && !string.IsNullOrWhiteSpace(_clientToken))
            {
                return existingClient;
            }

            throw new InvalidOperationException("Token refresh returned an empty token; cannot create Schema Registry client.");
        }

        await _clientLock.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            var tokenChanged = !string.Equals(_clientToken, token, StringComparison.Ordinal);
            var needsNewClient = _client == null || (tokenChanged && !string.IsNullOrWhiteSpace(token));

            if (!needsNewClient)
            {
                return _client!;
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                // Prefer the strongly-typed property rather than setting string keys.
                _config.BearerAuthToken = token;
            }

            var newClient = _clientFactory.Create(_config);
            var (uncachedRegistrar, effectiveRegistrar) = CreateRegistrars(newClient);

            var oldClient = _client;
            _client = newClient;
            _clientToken = token;
            _uncachedRegistrar = uncachedRegistrar;
            _registrarProxy.SetInner(effectiveRegistrar);

            oldClient?.Dispose();
            return newClient;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public async Task<string?> GetSchemaAsync(string subject, int version)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject cannot be null or empty", nameof(subject));

        var key = GetSubjectVersionKey(subject, version);
        if (TryGetFromCache(key, out var cached))
        {
            return cached?.Schema;
        }

        await GetClientAsync().ConfigureAwait(false);
        var registered = await (_uncachedRegistrar ?? Registrar).GetRegisteredSchemaAsync(subject, version).ConfigureAwait(false);

        SetCacheSubjectVersion(key, registered);

        return registered?.Schema;
    }

    public async Task<string?> GetSchema(byte[] message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (message.Length < 5) return null;
        if (message[0] != 0) return null;

        var id = (message[1] << 24) | (message[2] << 16) | (message[3] << 8) | message[4];
        if (id <= 0) return null;

        var key = GetIdKey(id);
        if (TryGetFromCache(key, out var cached))
        {
            return cached?.Schema;
        }

        await GetClientAsync().ConfigureAwait(false);
        var schema = await (_uncachedRegistrar ?? Registrar).GetSchemaAsync(id).ConfigureAwait(false);

        SetCacheId(key, id, schema);

        return schema?.SchemaString;
    }

    public async Task<int> RegisterSchemaAsync(
        string topicOrSubject,
        string schema,
        string schemaType = "AVRO",
        string? type = null,
        string? recordType = null)
    {
        if (string.IsNullOrWhiteSpace(topicOrSubject)) throw new ArgumentException("Topic/subject cannot be null or empty", nameof(topicOrSubject));
        if (string.IsNullOrWhiteSpace(schema)) throw new ArgumentException("Schema cannot be null or empty", nameof(schema));

        await GetClientAsync().ConfigureAwait(false);
        var subject = GetSubjectName(topicOrSubject, type, recordType);
        var parsedType = ParseSchemaType(schemaType);
        var schemaObj = new Schema(schema, parsedType);
        return await Registrar.RegisterSchemaAsync(subject, schemaObj).ConfigureAwait(false);
    }

    public Task<int> RegisterValueSchemaAsync(string topic, string schema, string schemaType = "AVRO", string? recordType = null)
        => RegisterSchemaAsync(topic, schema, schemaType, type: "value", recordType: recordType);

    public Task<int> RegisterKeySchemaAsync(string topic, string schema, string schemaType = "AVRO", string? recordType = null)
        => RegisterSchemaAsync(topic, schema, schemaType, type: "key", recordType: recordType);

    public SchemaType ParseSchemaType(string? schemaType)
    {
        if (string.IsNullOrWhiteSpace(schemaType)) return SchemaType.Avro;
        return Enum.TryParse<SchemaType>(schemaType, ignoreCase: true, out var parsed)
            ? parsed
            : SchemaType.Avro;
    }

    public string GetSubjectName(string topicOrSubject, string? type, string? recordType)
    {
        if (string.IsNullOrWhiteSpace(topicOrSubject)) throw new ArgumentException("Topic/subject cannot be null or empty", nameof(topicOrSubject));

        var custom = _options.SubjectNameStrategyImplementation;
        if (custom != null)
        {
            return custom.GetSubjectName(topicOrSubject, type, recordType);
        }

        var strategy = _options.SubjectNameStrategy;
        if (strategy.HasValue)
        {
            return strategy.Value switch
            {
                ModelSubjectNameStrategy.TopicName => TopicNameStrategy.GetSubjectName(topicOrSubject, type, recordType),
                ModelSubjectNameStrategy.TopicRecordName => TopicRecordNameStrategy.GetSubjectName(topicOrSubject, type, recordType),
                ModelSubjectNameStrategy.RecordName => RecordNameStrategy.GetSubjectName(topicOrSubject, type, recordType),
                _ => TopicNameStrategy.GetSubjectName(topicOrSubject, type, recordType)
            };
        }

        // Legacy behavior: if recordType is present, default to topic-record naming.
        if (!string.IsNullOrWhiteSpace(recordType))
        {
            return TopicRecordNameStrategy.GetSubjectName(topicOrSubject, type, recordType);
        }

        return TopicNameStrategy.GetSubjectName(topicOrSubject, type, recordType);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _client?.Dispose();
        }
        finally
        {
            _client = null;
            _clientLock.Dispose();

            if (TokenManager is IDisposable d)
            {
                d.Dispose();
            }
        }
    }

    private (ISchemaRegistrar uncachedRegistrar, ISchemaRegistrar effectiveRegistrar) CreateRegistrars(ISchemaRegistryClient client)
    {
        var baseRegistrar = new DefaultSchemaRegistrar(client);
        if (_cache == null)
        {
            return (baseRegistrar, baseRegistrar);
        }

        return (baseRegistrar, new CachingSchemaRegistrar(baseRegistrar, _cache));
    }

    private void ApplyConfluentCloudOAuthExtensions()
    {
        if (!string.IsNullOrWhiteSpace(_options.LogicalCluster))
        {
            _config.Set("bearer.auth.logical.cluster", _options.LogicalCluster);
        }

        if (!string.IsNullOrWhiteSpace(_options.IdentityPoolId))
        {
            _config.Set("bearer.auth.identity.pool.id", _options.IdentityPoolId);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SchemaRegistryExtClient));
        }
    }

    private static string GetSubjectVersionKey(string subject, int version) => $"{subject}:{version}";

    private static string GetIdKey(int id) => $"id:{id}";

    private bool TryGetFromCache(string key, out CachedSchemaInfo? cached)
    {
        cached = null;
        return _cache != null && _cache.TryGet(key, out cached);
    }

    private void SetCacheSubjectVersion(string key, CachedSchemaInfo? registered)
    {
        if (_cache == null) return;

        _cache.Set(key, registered);
        if (registered?.Id is int id)
        {
            _cache.Set(GetIdKey(id), registered);
        }
    }

    private void SetCacheId(string key, int id, Schema? schema)
    {
        if (_cache == null) return;

        if (schema == null)
        {
            _cache.Set(key, null);
            return;
        }

        _cache.Set(key, new CachedSchemaInfo
        {
            Subject = null,
            Id = id,
            Schema = schema.SchemaString,
            Type = null,
            SchemaType = schema.SchemaType.ToString()
        });
    }

    private sealed class RegistrarProxy : ISchemaRegistrar
    {
        private ISchemaRegistrar? _inner;

        public void SetInner(ISchemaRegistrar inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Task<CachedSchemaInfo?> GetRegisteredSchemaAsync(string subject, int version)
            => (_inner ?? throw new InvalidOperationException("Schema registrar is not initialized.")).GetRegisteredSchemaAsync(subject, version);

        public Task<Schema?> GetSchemaAsync(int id)
            => (_inner ?? throw new InvalidOperationException("Schema registrar is not initialized.")).GetSchemaAsync(id);

        public Task<int> RegisterSchemaAsync(string subject, Schema schema)
            => (_inner ?? throw new InvalidOperationException("Schema registrar is not initialized.")).RegisterSchemaAsync(subject, schema);

        public Task DeleteSchemaVersionAsync(string subject, int version)
            => (_inner ?? throw new InvalidOperationException("Schema registrar is not initialized.")).DeleteSchemaVersionAsync(subject, version);
    }
}


