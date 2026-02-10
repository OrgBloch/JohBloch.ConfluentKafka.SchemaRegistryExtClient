# SRTimerFunction (sample)

Azure Functions (.NET isolated) sample showing how to:
- Register `JohBloch.ConfluentKafka.SchemaRegistryExtClient` via a reusable `IServiceCollection` extension
- Read config from environment variables / `local.settings.json`
- Use MSAL client credentials to refresh Confluent Schema Registry OAuth tokens

## Local run

1) Copy the example settings:

- Copy `local.settings.json.example` to `local.settings.json`
- Fill in your own values

2) Run the Functions host from this folder:

```bash
func start
```

Notes:
- `local.settings.json` is ignored by git via `.gitignore`.
- In Azure, these settings should be configured as App Settings instead.
