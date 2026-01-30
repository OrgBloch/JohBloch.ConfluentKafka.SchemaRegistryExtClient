using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Models;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSchemaRegistryExtClient(this IServiceCollection services, SchemaRegistryConfig config, Func<Task<(string token, DateTime expiresAt)>>? tokenRefreshFunc = null, Action<SchemaClientOptions>? configure = null, bool enableOpenTelemetryMetrics = false)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var options = new SchemaClientOptions();
            configure?.Invoke(options);

            // register options and cache
            services.AddSingleton(options);
            if (enableOpenTelemetryMetrics)
            {
                services.AddSingleton<IMetricsCollector, OpenTelemetryMetricsCollector>();
            }
            else
            {
                services.AddSingleton<IMetricsCollector, DefaultMetricsCollector>();
            }

            services.AddSingleton<ISchemaCache>(sp => new InMemorySchemaCache(options.CacheOptions, sp.GetService<ILogger<InMemorySchemaCache>>(), sp.GetService<IMetricsCollector>()));

            // Register the client factory and token manager (if tokenRefreshFunc provided)
            services.AddSingleton<ISchemaRegistryClientFactory, DefaultSchemaRegistryClientFactory>();
            if (tokenRefreshFunc != null)
            {
                services.AddSingleton<ITokenManager>(sp => new TokenManager(tokenRefreshFunc, sp.GetService<IMetricsCollector>()));
            }

            // Register the extended client as the main service (singleton) so it can manage token refresh and registrar reuse
            if (tokenRefreshFunc != null)
            {
                // Use the registered ITokenManager instance so DI and the client share the same instance
                services.AddSingleton<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>(sp => new JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient(config, sp.GetRequiredService<ITokenManager>(), sp.GetRequiredService<ISchemaCache>(), options, sp.GetRequiredService<ISchemaRegistryClientFactory>()));
            }
            else
            {
                services.AddSingleton<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>(sp => new JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient(config, (Interfaces.ITokenManager?)null, sp.GetRequiredService<ISchemaCache>(), options, sp.GetRequiredService<ISchemaRegistryClientFactory>()));
            }

            // Expose common interfaces
            services.AddSingleton<ISchemaRegistryExtClient>(sp => sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>());
            services.AddSingleton<ISchemaRegistrar>(sp => sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>().Registrar);

            // If TokenManager was registered, it's already exposed above; avoid circular registration.
            return services;
        }
    }
}