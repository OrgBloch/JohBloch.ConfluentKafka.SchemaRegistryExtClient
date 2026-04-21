using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SampleApp.Extensions;

public static class KafkaServiceCollectionExtensions
{
    /// <summary>
    /// Registers:
    /// - Schema Registry ext client (JohBloch.ConfluentKafka.SchemaRegistryExtClient)
    /// - Kafka producer: IProducer&lt;string, byte[]&gt; (singleton)
    /// - Kafka consumer factory: Func&lt;IConsumer&lt;string, byte[]&gt;&gt; (creates a new consumer per call)
    ///
    /// Settings can be provided as environment variables or via Azure Functions-style local.settings.json (Values:...)
    /// </summary>
    public static IServiceCollection AddKafkaProducerConsumerAndSchemaRegistryFromConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        bool enableOpenTelemetryMetrics = false,
        Action<ProducerConfig>? configureProducer = null,
        Action<ConsumerConfig>? configureConsumer = null,
        Action<SchemaClientOptions>? configureSchemaOptions = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        static string? GetSetting(IConfiguration cfg, string key)
            => cfg[key] ?? cfg[$"Values:{key}"];

        static string GetRequired(IConfiguration cfg, string key)
            => GetSetting(cfg, key) ?? throw new InvalidOperationException($"Missing setting: {key}");

        // --- Schema Registry ---
        var schemaRegistryUrl = GetSetting(configuration, "SCHEMA_REGISTRY_URL") ?? "http://localhost:8081";
        var srConfig = new SchemaRegistryConfig { Url = schemaRegistryUrl };

        var apiKey = GetSetting(configuration, "SCHEMA_REGISTRY_API_KEY");
        var apiSecret = GetSetting(configuration, "SCHEMA_REGISTRY_API_SECRET");

        var tokenEndpoint = GetSetting(configuration, "OAUTH_TOKEN_ENDPOINT");
        var oauthClientId = GetSetting(configuration, "OAUTH_CLIENT_ID");
        var oauthClientSecret = GetSetting(configuration, "OAUTH_CLIENT_SECRET");
        var oauthScope = GetSetting(configuration, "OAUTH_SCOPE");

        var logicalCluster = GetSetting(configuration, "OAUTH_LOGICAL_CLUSTER");
        var identityPoolId = GetSetting(configuration, "OAUTH_IDENTITY_POOL_ID");

        Func<Task<(string token, DateTime expiresAt)>>? tokenRefreshFunc = null;
        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
        {
            // API key auth for Confluent Cloud
            srConfig.BasicAuthUserInfo = $"{apiKey}:{apiSecret}";
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

                using var resp = await http.PostAsync(tokenEndpoint!, new FormUrlEncodedContent(form)).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                var payload = await resp.Content.ReadFromJsonAsync<JsonDocument>().ConfigureAwait(false);
                var accessToken = payload!.RootElement.GetProperty("access_token").GetString()!;
                var expiresIn = payload.RootElement.TryGetProperty("expires_in", out var expiresInProp)
                    ? expiresInProp.GetInt32()
                    : 3600;

                return (accessToken, DateTime.UtcNow.AddSeconds(expiresIn));
            };
        }

        services.AddSchemaRegistryExtClient(
            srConfig,
            tokenRefreshFunc,
            opts =>
            {
                // Default: keep consistent with Confluent serializer defaults when you use topic+record naming
                opts.SubjectNameStrategy = JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models.SubjectNameStrategy.TopicRecordName;

                // Confluent Cloud OAuth extensions (optional)
                opts.LogicalCluster = logicalCluster;
                opts.IdentityPoolId = identityPoolId;

                configureSchemaOptions?.Invoke(opts);
            },
            enableOpenTelemetryMetrics: enableOpenTelemetryMetrics);

        // --- Kafka producer/consumer ---
        var bootstrapServers = GetRequired(configuration, "KAFKA_BOOTSTRAP_SERVERS");

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = GetSetting(configuration, "KAFKA_CLIENT_ID")
        };
        configureProducer?.Invoke(producerConfig);
        services.AddSingleton(producerConfig);

        services.AddSingleton<IProducer<string, byte[]>>(sp =>
        {
            var cfg = sp.GetRequiredService<ProducerConfig>();
            return new ProducerBuilder<string, byte[]>(cfg)
                .SetKeySerializer(Serializers.Utf8)
                .SetValueSerializer(Serializers.ByteArray)
                .Build();
        });

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = GetRequired(configuration, "KAFKA_GROUP_ID"),
            ClientId = GetSetting(configuration, "KAFKA_CLIENT_ID"),
            EnableAutoCommit = true,
            AutoOffsetReset = Enum.TryParse<AutoOffsetReset>(GetSetting(configuration, "KAFKA_AUTO_OFFSET_RESET"), ignoreCase: true, out var aor)
                ? aor
                : AutoOffsetReset.Earliest
        };
        configureConsumer?.Invoke(consumerConfig);
        services.AddSingleton(consumerConfig);

        // Consumers are not thread-safe; create a new instance per consuming loop.
        services.AddSingleton<Func<IConsumer<string, byte[]>>>(sp =>
        {
            var cfg = sp.GetRequiredService<ConsumerConfig>();
            return () => new ConsumerBuilder<string, byte[]>(cfg)
                .SetKeyDeserializer(Deserializers.Utf8)
                .SetValueDeserializer(Deserializers.ByteArray)
                .Build();
        });

        return services;
    }
}
