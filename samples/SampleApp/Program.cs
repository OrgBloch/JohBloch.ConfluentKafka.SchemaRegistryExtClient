using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;

await Task.Yield(); // top-level await friendly

Console.WriteLine("SampleApp starting...");

// Configure OpenTelemetry MeterProvider to export Console metrics
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("JohBloch.SchemaRegistryExtClient")
    .AddConsoleExporter()
    .Build();

var host = Host.CreateDefaultBuilder()
    .ConfigureLogging(logging => logging.AddConsole())
    .ConfigureServices((ctx, services) =>
    {
        // Replace with your real registry URL
        var config = new SchemaRegistryConfig { Url = "http://localhost:8081" };

        // Minimal token refresh function (replace with your OAuth client flow in production)
        async Task<(string token, DateTime expiresAt)> TokenRefreshAsync()
        {
            // Example: call your identity provider and return token + expiry
            await Task.Delay(10);
            return ("sample-token-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(10));
        }

        services.AddSchemaRegistryExtClient(config, TokenRefreshAsync, options =>
        {
            options.SubjectNameStrategy = SubjectNameStrategy.TopicRecordName;
        }, enableOpenTelemetryMetrics: true);
    })
    .Build();

using var sp = host.Services;
var client = sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>();
var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SampleApp");

try
{
    var subject = "sample-topic-value";
    var schema = "{\"type\":\"string\"}";

    logger.LogInformation("Registering schema for subject {subject}", subject);
    var id = await client.RegisterSchemaAsync(subject, schema);
    logger.LogInformation("Registered schema id: {id}", id);

    var fetched = await client.GetSchemaAsync(subject, 1);
    logger.LogInformation("Fetched schema: {schema}", fetched ?? "(null)");
}
catch (Exception ex)
{
    logger.LogError(ex, "Could not call Schema Registry (this sample is safe to run without a running registry)\nStart a local registry and rerun the sample to exercise full flow.");
}

Console.WriteLine("SampleApp finished.");
