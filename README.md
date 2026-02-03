# JohBloch.ConfluentKafka.SchemaRegistryExtClient

Extended client library for Confluent Kafka Schema Registry with DI integration, caching, token refresh support, and optional OpenTelemetry metrics.

## Features
- Targets `net8.0` and `net10.0`
- Easy integration with Microsoft DI (`AddSchemaRegistryExtClient`)
- Pluggable schema cache: in-memory by default, or replace with an external/distributed cache (useful for Azure Functions and other scale-out hosts)
- Optional token refresh (OAuth/custom bearer token) via delegate or DI-provided `ITokenManager` (keeps a singleton client alive for a long time)
- Subject naming strategies aligned with Confluent serializer configuration
- Optional OpenTelemetry-compatible metrics

## What is “extended” compared to Confluent’s client?
`SchemaRegistryExtClient` wraps Confluent’s Schema Registry client and adds:

- A caching layer (`ISchemaCache`) so repeated lookups/registrations don’t always hit Schema Registry.
- A token refresh mechanism (`ITokenManager` / token refresh delegate) so the client can run as a long-lived singleton without restarting when tokens expire.
- DI-friendly registration so you can inject `ISchemaRegistryExtClient` / `ISchemaRegistrar` in your app.

## Caching (in-memory vs external)
By default, `AddSchemaRegistryExtClient` registers an in-memory cache (`InMemorySchemaCache`). This is great for single-process apps.

If you run in a scale-out environment (for example Azure Functions with multiple instances), you typically want an external/distributed cache so cache entries are shared across instances. You can do that by implementing `ISchemaCache` and overriding the DI registration:

```csharp
services.AddSchemaRegistryExtClient(config, tokenRefreshFunc: null);

// Override the default in-memory cache registration (last registration wins).
services.AddSingleton<ISchemaCache>(sp => new MyExternalSchemaCache(/* Redis, etc. */));
```

The cache stores schema lookups (by subject/version and by schema id) and supports TTL and negative caching.

### Example: Redis-backed cache (StackExchange.Redis)

This is a minimal example of using Redis as the shared cache (useful for Azure Functions / multiple instances).

1) Add package:

```bash
dotnet add package StackExchange.Redis
```

2) Register the client, Redis connection, and override `ISchemaCache`:

```csharp
using System.Text.Json;
using StackExchange.Redis;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;

services.AddSchemaRegistryExtClient(config, tokenRefreshFunc: null, configure: opts =>
{
  opts.CacheOptions.TimeToLiveSeconds = 300;
});

services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect("localhost:6379"));

// Override the default in-memory cache registration (last registration wins).
services.AddSingleton<ISchemaCache>(sp =>
  new RedisSchemaCache(
    sp.GetRequiredService<IConnectionMultiplexer>(),
    sp.GetRequiredService<SchemaClientOptions>().CacheOptions,
    prefix: "schema-registry:"));

sealed class RedisSchemaCache : ISchemaCache
{
  private readonly IDatabase _db;
  private readonly SchemaCacheOptions _options;
  private readonly string _prefix;
  private int _hitCount;
  private int _missCount;

  public RedisSchemaCache(IConnectionMultiplexer mux, SchemaCacheOptions options, string prefix)
  {
    _db = mux.GetDatabase();
    _options = options ?? new SchemaCacheOptions();
    _prefix = string.IsNullOrWhiteSpace(prefix) ? "schema-registry:" : prefix;
  }

  public int HitCount => _hitCount;
  public int MissCount => _missCount;
  public int Count => -1; // Redis doesn't expose a cheap count per prefix
  public event EventHandler<string?>? CacheHit;
  public event EventHandler<string?>? CacheMiss;

  private string K(string key) => _prefix + key;

  public bool TryGet(string? key, out CachedSchemaInfo? schema)
  {
    schema = null;
    if (string.IsNullOrWhiteSpace(key))
    {
      Interlocked.Increment(ref _missCount);
      CacheMiss?.Invoke(this, key);
      return false;
    }

    var val = _db.StringGet(K(key));
    if (!val.HasValue)
    {
      Interlocked.Increment(ref _missCount);
      CacheMiss?.Invoke(this, key);
      return false;
    }

    // Supports negative caching by allowing null payload.
    schema = val == "null" ? null : JsonSerializer.Deserialize<CachedSchemaInfo>(val!);
    Interlocked.Increment(ref _hitCount);
    CacheHit?.Invoke(this, key);
    return true;
  }

  public void Set(string? key, CachedSchemaInfo? schema)
  {
    if (string.IsNullOrWhiteSpace(key)) return;
    var ttl = TimeSpan.FromSeconds(_options.TimeToLiveSeconds);
    var payload = schema == null ? "null" : JsonSerializer.Serialize(schema);
    _db.StringSet(K(key), payload, ttl);
  }

  public void Remove(string? key)
  {
    if (string.IsNullOrWhiteSpace(key)) return;
    _db.KeyDelete(K(key));
  }

  public void Clear()
  {
    // Clearing by prefix requires server-side key scan; keep it host-specific.
    // Many apps skip implementing this in Redis-backed caches.
  }

  public IEnumerable<string> KeysMatchingPrefix(string prefix) => Array.Empty<string>();

  public void Dispose() { }
}
```

Notes:
- `KeysMatchingPrefix` / `Clear` are intentionally left as no-ops in this minimal sample because prefix scans can be expensive in Redis.
- For production, prefer key versioning (prefix changes) instead of scanning deletes.

## Token refresh (long-lived clients)
Schema Registry auth tokens often expire. If you pass a token refresh delegate (`Func<Task<(string token, DateTime expiresAt)>>`) or register an `ITokenManager`, the client will refresh tokens when needed and recreate the underlying Confluent client when the bearer token changes.

This enables running `SchemaRegistryExtClient` as a singleton in DI for days/weeks without manual recycling.

### What if I don't provide a token refresh method?
Nothing special happens: the library will not create/use a `TokenManager`, and it will call Schema Registry using only what you configured on `SchemaRegistryConfig` (for example API key / basic auth).

- If your Schema Registry uses **OAuth/Bearer tokens**, you must provide `tokenRefreshFunc` (or register your own `ITokenManager`) or requests will fail with auth errors.
- If you use **API Key / Basic Auth**, you can omit token refresh entirely:

```csharp
var config = new SchemaRegistryConfig { Url = "https://your-registry" };
config.BasicAuthUserInfo = "<API_KEY>:<API_SECRET>";

services.AddSchemaRegistryExtClient(config, tokenRefreshFunc: null);
```

## Getting Started
See [docs/usage.md](docs/usage.md) for examples of connecting to Schema Registry with API Key, OAuth, or custom token refresh.

## Subject Name Strategy
You can configure how subjects are derived by the client using `SchemaClientOptions.SubjectNameStrategy`. This aligns subject naming with serializer configuration (for example, the serializer setting `subject.name.strategy`).

- **TopicName** — subjects are `topic-key` or `topic-value`.
- **TopicRecordName** — subjects are `topic-recordType` when a `recordType` is provided.
- **RecordName** — subjects are the `recordType` itself.

If `SubjectNameStrategy` is not set, the client uses a legacy fallback: prefer `topic-record` when `recordType` is provided, otherwise `topic-key` / `topic-value`.

Example:

```csharp
var options = new SchemaClientOptions
{
    SubjectNameStrategy = SubjectNameStrategy.TopicRecordName
};
var client = new SchemaRegistryExtClient(config, tokenRefreshFunc, cache, options);
```

If you need custom subject naming logic, you can provide a concrete implementation via `SchemaClientOptions.SubjectNameStrategyImplementation` (this takes precedence over the enum).

If you are using Confluent serializers, ensure the serializer's `subject.name.strategy` setting matches this option to avoid subject mismatches.

## Supported Authentication Methods
- API Key (Basic Auth/SCRAM, Confluent Cloud)
- OAuth Bearer Token (Client Credentials)
- Custom Bearer Token (manual refresh)

## Usage examples (🔐 Authentication)

### local.settings.json templates

If you're using Azure Functions (or another host that supports `local.settings.json`-style local secrets), here are ready-to-use templates for the two most common setups.

OAuth2 (Client Credentials):

```json
{
  "IsEncrypted": false,
  "Values": {
    "SCHEMA_REGISTRY_URL": "https://your-registry.example.com",
    "OAUTH_TOKEN_ENDPOINT": "https://identity.example.com/oauth2/token",
    "OAUTH_CLIENT_ID": "your-client-id",
    "OAUTH_CLIENT_SECRET": "your-client-secret",
    "OAUTH_SCOPE": "registry.write",
    "OAUTH_LOGICAL_CLUSTER": "lkc-xxxxx",
    "OAUTH_IDENTITY_POOL_ID": "pool-xxxxx"
  }
}
```

API Key (Confluent Cloud / Basic Auth):

```json
{
  "IsEncrypted": false,
  "Values": {
    "SCHEMA_REGISTRY_URL": "https://your-confluent-cloud-registry",
    "SCHEMA_REGISTRY_API_KEY": "<API_KEY>",
    "SCHEMA_REGISTRY_API_SECRET": "<API_SECRET>"
  }
}
```

### 1) OAuth2 (Client Credentials / Bearer token)

```csharp
// Minimal example using DI + the library's built-in TokenManager (registered as ITokenManager)
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;

var config = new SchemaRegistryConfig
{
  Url = Environment.GetEnvironmentVariable("SCHEMA_REGISTRY_URL") ?? "https://your-registry.example.com"
};

// You only provide the refresh function. The library will wrap it in the built-in TokenManager.
// No initial access token is required — the TokenManager calls this function on-demand
// (first operation / near expiry) and caches the token until it needs refreshing.
Func<Task<(string token, DateTime expiresAt)>> tokenRefreshFunc = async () =>
{
  using var http = new HttpClient();

  static string Env(string key) => Environment.GetEnvironmentVariable(key)
    ?? throw new InvalidOperationException($"Missing env var: {key}");
  static string? EnvOpt(string key) => Environment.GetEnvironmentVariable(key);

  var form = new Dictionary<string, string>
  {
    ["grant_type"] = "client_credentials",
    ["client_id"] = Env("OAUTH_CLIENT_ID"),
    ["client_secret"] = Env("OAUTH_CLIENT_SECRET"),
  };
  var scope = EnvOpt("OAUTH_SCOPE");
  if (!string.IsNullOrWhiteSpace(scope)) form["scope"] = scope;

  using var resp = await http.PostAsync(Env("OAUTH_TOKEN_ENDPOINT"), new FormUrlEncodedContent(form));
  resp.EnsureSuccessStatusCode();

  var payload = await resp.Content.ReadFromJsonAsync<JsonDocument>();
  var accessToken = payload!.RootElement.GetProperty("access_token").GetString()!;
  var expiresIn = payload.RootElement.TryGetProperty("expires_in", out var p) ? p.GetInt32() : 3600;
  return (accessToken, DateTime.UtcNow.AddSeconds(expiresIn));
};

var services = new ServiceCollection();
services.AddSchemaRegistryExtClient(
  config,
  tokenRefreshFunc,
  opts =>
  {
    // Required for some Confluent Cloud OAuth setups
    opts.LogicalCluster = Environment.GetEnvironmentVariable("OAUTH_LOGICAL_CLUSTER");
    opts.IdentityPoolId = Environment.GetEnvironmentVariable("OAUTH_IDENTITY_POOL_ID");
  });

var sp = services.BuildServiceProvider();

// Use the extended client (it will fetch/refresh tokens as needed)
var client = sp.GetRequiredService<ISchemaRegistryExtClient>();
```

> Tip: You can also use MSAL or IdentityModel to get tokens and expose a `Func<Task<(string token, DateTime expiresAt)>>` accordingly.

See the "local.settings.json templates" section above.

Use these values in your token refresh implementation (or load them into environment variables before running tests/app):

- PowerShell: $env:SCHEMA_REGISTRY_URL = (Get-Content local.settings.json | ConvertFrom-Json).Values.SCHEMA_REGISTRY_URL
- Bash: export SCHEMA_REGISTRY_URL=$(jq -r '.Values.SCHEMA_REGISTRY_URL' local.settings.json)

---

### 2) API Key (Confluent Cloud / Basic Auth)

```csharp
// Confluent Cloud: use the API key and secret as basic auth credentials
var config = new SchemaRegistryConfig { Url = "https://your-confluent-cloud-registry" };
// Option A: set BasicAuthUserInfo property
config.BasicAuthUserInfo = "<API_KEY>:<API_SECRET>";

// Option B: set via config.Set
config.Set("basic.auth.user.info", "<API_KEY>:<API_SECRET>");

var client = new SchemaRegistryExtClient(config);
```

See the "local.settings.json templates" section above.

To use the local settings values for the API key sample, either set `BasicAuthUserInfo` from the `SCHEMA_REGISTRY_API_KEY`/`SCHEMA_REGISTRY_API_SECRET` environment variables, or load them into the environment using the PowerShell/Bash examples above.

---

## OpenTelemetry metrics (📈)
You can enable OpenTelemetry-compatible metrics when registering services with DI using `AddSchemaRegistryExtClient(..., enableOpenTelemetryMetrics: true)`; see `docs/usage.md` for details.

## License
MIT