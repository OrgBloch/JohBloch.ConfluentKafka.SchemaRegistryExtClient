# SampleApp

Minimal runnable sample demonstrating:
- OAuth2-like token refresh via a `Func<Task<(string token, DateTime expiresAt)>>` passed to the client
- DI registration via `AddSchemaRegistryExtClient(...)`
- Enabling OpenTelemetry metrics and exporting to console

How to run:

1. Open a terminal at `samples/SampleApp`
2. Create a `local.settings.json` file in the sample folder (you can start from `local.settings.json.template`).
3. `dotnet run`

The sample will attempt to call a Schema Registry at `http://localhost:8081`. If none is available the sample logs will show the error but the app will still run (safe default behavior).

The app automatically loads settings from `local.settings.json` (Azure Functions style) if the file exists, but environment variables also work.

Example `local.settings.json` (OAuth):

```json
{
  "IsEncrypted": false,
  "Values": {
    "SCHEMA_REGISTRY_URL": "http://localhost:8081",
    "OAUTH_TOKEN_ENDPOINT": "https://identity.example.com/oauth2/token",
    "OAUTH_CLIENT_ID": "your-client-id",
    "OAUTH_CLIENT_SECRET": "your-client-secret",
    "OAUTH_SCOPE": "registry.write",
    "OAUTH_LOGICAL_CLUSTER": "lkc-xxxxx",
    "OAUTH_IDENTITY_POOL_ID": "pool-xxxxx"
  }
}
```

Example `local.settings.json` (API Key):

```json
{
  "IsEncrypted": false,
  "Values": {
    "SCHEMA_REGISTRY_URL": "http://localhost:8081",
    "SCHEMA_REGISTRY_API_KEY": "<API_KEY>",
    "SCHEMA_REGISTRY_API_SECRET": "<API_SECRET>"
  }
}
```

Optional: load `local.settings.json` values into environment variables (PowerShell):

```powershell
$json = Get-Content local.settings.json | ConvertFrom-Json
$json.Values.GetEnumerator() | ForEach-Object { $env:$($_.Name) = $_.Value }
```

(Bash):

```bash
export $(jq -r '.Values | to_entries| .[] | "\(.key)=\(.value)"' local.settings.json)
```

Then `dotnet run` will pick up `SCHEMA_REGISTRY_URL` and the sample will use the configured auth values.