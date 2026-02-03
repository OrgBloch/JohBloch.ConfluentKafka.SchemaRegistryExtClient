# JohBloch.ConfluentKafka.SchemaRegistryExtClient

Extended client library for Confluent Kafka Schema Registry with DI integration, caching, token refresh support, and optional OpenTelemetry metrics.

## Features
- Targets `net8.0` and `net10.0`
- Easy integration with Microsoft DI (`AddSchemaRegistryExtClient`)
- Optional token refresh (OAuth/custom bearer token) via delegate or DI-provided `ITokenManager`
- Subject naming strategies aligned with Confluent serializer configuration
- Optional OpenTelemetry-compatible metrics

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
    "OAUTH_SCOPE": "registry.write"
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
// Minimal example using a token refresh function (client credentials flow)
using System.Net.Http.Json;
using System.Text.Json;

var config = new SchemaRegistryConfig { Url = "https://your-registry.example.com" };

async Task<(string token, DateTime expiresAt)> TokenRefreshAsync()
{
    using var http = new HttpClient();
    var tokenEndpoint = "https://identity.example.com/oauth2/token";

    var resp = await http.PostAsJsonAsync(tokenEndpoint, new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = "your-client-id",
        ["client_secret"] = "your-client-secret",
        ["scope"] = "registry.write"
    });
    resp.EnsureSuccessStatusCode();

    var payload = await resp.Content.ReadFromJsonAsync<JsonDocument>();
    var accessToken = payload!.RootElement.GetProperty("access_token").GetString()!;
    var expiresIn = payload.RootElement.GetProperty("expires_in").GetInt32();
    return (accessToken, DateTime.UtcNow.AddSeconds(expiresIn));
}

// Pass the token refresh func when constructing the client — the client will call it as needed
var client = new SchemaRegistryExtClient(config, TokenRefreshAsync);
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