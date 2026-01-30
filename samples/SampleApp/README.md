# SampleApp

Minimal runnable sample demonstrating:
- OAuth2-like token refresh via a `Func<Task<(string token, DateTime expiresAt)>>` passed to the client
- DI registration via `AddSchemaRegistryExtClient(...)`
- Enabling OpenTelemetry metrics and exporting to console

How to run:

1. Open a terminal at `samples/SampleApp`
2. Create a `local.settings.json` file in the sample folder (example below) or set equivalent environment variables.
3. dotnet run

The sample will attempt to call a Schema Registry at `http://localhost:8081`. If none is available the sample logs will show the error but the app will still run (safe default behavior). Replace `TokenRefreshAsync` with your real OAuth flow in production (MSAL/IdentityModel examples are mentioned in the main README).

Example `local.settings.json` (OAuth):

```json
{
  "IsEncrypted": false,
  "Values": {
    "SCHEMA_REGISTRY_URL": "http://localhost:8081",
    "OAUTH_TOKEN_ENDPOINT": "https://identity.example.com/oauth2/token",
    "OAUTH_CLIENT_ID": "your-client-id",
    "OAUTH_CLIENT_SECRET": "your-client-secret",
    "OAUTH_SCOPE": "registry.write"
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

Load local.settings values into environment variables (PowerShell):

```powershell
$json = Get-Content local.settings.json | ConvertFrom-Json
$json.Values.GetEnumerator() | ForEach-Object { $env:$($_.Name) = $_.Value }
```

(Bash):

```bash
export $(jq -r '.Values | to_entries| .[] | "\(.key)=\(.value)"' local.settings.json)
```

Then `dotnet run` will pick up `SCHEMA_REGISTRY_URL` and the sample will use the configured auth values.