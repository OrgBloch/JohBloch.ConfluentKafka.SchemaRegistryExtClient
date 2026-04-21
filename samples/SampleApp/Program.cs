using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Confluent.Kafka;
using SampleApp.Extensions;

await Task.Yield(); // top-level await friendly

Console.WriteLine("SampleApp starting...");

// Configure OpenTelemetry MeterProvider to export Console metrics
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("JohBloch.SchemaRegistryExtClient")
    .AddConsoleExporter()
    .Build();

var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration((ctx, config) =>
    {
        // Support Azure Functions-style local settings for the sample.
        // Keys become available under "Values:<KEY>".
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .ConfigureServices((ctx, services) =>
    {
        services.AddKafkaProducerConsumerAndSchemaRegistryFromConfiguration(
            ctx.Configuration,
            enableOpenTelemetryMetrics: true);
    })
    .Build();

var sp = host.Services;
var client = sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>();
var producer = sp.GetRequiredService<IProducer<string, byte[]>>();
var consumerFactory = sp.GetRequiredService<Func<IConsumer<string, byte[]>>>();
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
