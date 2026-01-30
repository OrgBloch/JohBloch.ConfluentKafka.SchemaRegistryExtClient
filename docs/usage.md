# Usage

This page contains examples for connecting to Schema Registry and configuring the client.

## Subject Naming and `SubjectNameStrategy`
The client exposes the `SchemaClientOptions.SubjectNameStrategy` option to align subject naming with serializer configuration.

Examples:

- TopicRecordName (subject = `topic-recordType` when recordType provided):

```csharp
var options = new SchemaClientOptions { SubjectNameStrategy = SubjectNameStrategy.TopicRecordName };
var client = new SchemaRegistryExtClient(config, null, null, options);
await client.RegisterKeySchemaAsync("orders", schemaString, "AVRO", "OrderKey");
// Subject used: "orders-OrderKey"
```

- TopicName (subject = `topic-key` / `topic-value`):

```csharp
var options = new SchemaClientOptions { SubjectNameStrategy = SubjectNameStrategy.TopicName };
var client = new SchemaRegistryExtClient(config, null, null, options);
await client.RegisterValueSchemaAsync("orders", schemaString);
// Subject used: "orders-value"
```

- RecordName (subject = `recordType`):

```csharp
var options = new SchemaClientOptions { SubjectNameStrategy = SubjectNameStrategy.RecordName };
var client = new SchemaRegistryExtClient(config, null, null, options);
await client.RegisterSchemaAsync("orders", schemaString, "AVRO", "value", "OrderValue");
// Subject used: "OrderValue"
```

- Legacy default (when `SubjectNameStrategy` is null): prefer `topic-record` when `recordType` present, otherwise `topic-key` / `topic-value`.

Note: When using Confluent serializers, please set the serializer's `subject.name.strategy` to match your chosen `SubjectNameStrategy` to avoid subject mismatches between serializer and registry operations.

---

## Integration with Microsoft DI (⚙️)

Register the client and related services with `IServiceCollection`:

```csharp
var services = new ServiceCollection();
services.AddSchemaRegistryExtClient(config, tokenRefreshFunc: null, opts =>
{
    opts.SubjectNameStrategy = SubjectNameStrategy.TopicRecordName;
});
var sp = services.BuildServiceProvider();

var client = sp.GetRequiredService<ISchemaRegistryExtClient>();
var registrar = sp.GetRequiredService<ISchemaRegistrar>();
```

This registers `SchemaRegistryExtClient` as a singleton and exposes `ISchemaRegistryExtClient` and `ISchemaRegistrar` for consumption.

---

## OpenTelemetry metrics (📈)

The library emits metrics via the `JohBloch.SchemaRegistryExtClient` meter using these metric names:

- `schema_registry.cache.hit`
- `schema_registry.cache.miss`
- `schema_registry.cache.set`
- `schema_registry.token.refresh`

Enable the built-in OpenTelemetry-aware collector when registering services:

```csharp
var services = new ServiceCollection();
services.AddSchemaRegistryExtClient(config, tokenRefreshFunc: null, configure: null, enableOpenTelemetryMetrics: true);

// In your application, configure OpenTelemetry to collect metrics from the Meter:
// using OpenTelemetry.Metrics;
// Sdk.CreateMeterProviderBuilder().AddMeter("JohBloch.SchemaRegistryExtClient").AddConsoleExporter().Build();
```

When `enableOpenTelemetryMetrics` is true, the library registers an `IMetricsCollector` that uses `System.Diagnostics.Metrics` (Meter) so any OpenTelemetry `MeterProvider` configured by the host will pick up the emitted metrics.