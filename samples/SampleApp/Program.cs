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
    .ConfigureAppConfiguration((ctx, config) =>
    {
        // Support Azure Functions-style local settings for the sample.
        // Keys become available under "Values:<KEY>".
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureLogging(logging => logging.AddConsole())
    .ConfigureServices((ctx, services) =>
    {
        static string? GetSetting(IConfiguration configuration, string key)
            => configuration[key] ?? configuration[$"Values:{key}"];

        var schemaRegistryUrl = GetSetting(ctx.Configuration, "SCHEMA_REGISTRY_URL") ?? "http://localhost:8081";
        var config = new SchemaRegistryConfig { Url = schemaRegistryUrl };

        var apiKey = GetSetting(ctx.Configuration, "SCHEMA_REGISTRY_API_KEY");
        var apiSecret = GetSetting(ctx.Configuration, "SCHEMA_REGISTRY_API_SECRET");

        var tokenEndpoint = GetSetting(ctx.Configuration, "OAUTH_TOKEN_ENDPOINT");
        var oauthClientId = GetSetting(ctx.Configuration, "OAUTH_CLIENT_ID");
        var oauthClientSecret = GetSetting(ctx.Configuration, "OAUTH_CLIENT_SECRET");
        var oauthScope = GetSetting(ctx.Configuration, "OAUTH_SCOPE");

        var logicalCluster = GetSetting(ctx.Configuration, "OAUTH_LOGICAL_CLUSTER");
        var identityPoolId = GetSetting(ctx.Configuration, "OAUTH_IDENTITY_POOL_ID");

        Func<Task<(string token, DateTime expiresAt)>>? tokenRefreshFunc = null;
        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
        {
            // API key auth for Confluent Cloud
            config.BasicAuthUserInfo = $"{apiKey}:{apiSecret}";
        }
        else if (!string.IsNullOrWhiteSpace(tokenEndpoint)
            && !string.IsNullOrWhiteSpace(oauthClientId)
            && !string.IsNullOrWhiteSpace(oauthClientSecret))
        {
            // OAuth2 client-credentials flow (minimal sample)
            tokenRefreshFunc = async () =>
            {
                using var http = new HttpClient();
                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = oauthClientId!,
                    ["client_secret"] = oauthClientSecret!
                };
                if (!string.IsNullOrWhiteSpace(oauthScope))
                {
                    form["scope"] = oauthScope!;
                }

                using var resp = await http.PostAsync(tokenEndpoint!, new FormUrlEncodedContent(form));
                resp.EnsureSuccessStatusCode();

                var payload = await resp.Content.ReadFromJsonAsync<JsonDocument>();
                var accessToken = payload!.RootElement.GetProperty("access_token").GetString()!;
                var expiresIn = payload.RootElement.TryGetProperty("expires_in", out var expiresInProp)
                    ? expiresInProp.GetInt32()
                    : 3600;

                return (accessToken, DateTime.UtcNow.AddSeconds(expiresIn));
            };
        }

        services.AddSchemaRegistryExtClient(
            config,
            tokenRefreshFunc,
            opts =>
            {
                opts.SubjectNameStrategy = JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.TopicRecordName;
                opts.LogicalCluster = logicalCluster;
                opts.IdentityPoolId = identityPoolId;
            },
            enableOpenTelemetryMetrics: true);
    })
    .Build();

var sp = host.Services;
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
