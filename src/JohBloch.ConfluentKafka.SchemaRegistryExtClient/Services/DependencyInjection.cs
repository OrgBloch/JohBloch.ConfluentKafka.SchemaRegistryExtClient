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
            => AddSchemaRegistryExtClientInternal(services, config, tokenRefreshFunc, tokenRefreshFromServiceProvider: null, configure, enableOpenTelemetryMetrics);

        /// <summary>
        /// Registers <see cref="SchemaRegistryExtClient"/> and related services, using a token refresh function that can be built from DI.
        /// This enables using services like <c>IHttpClientFactory</c> inside the refresh implementation.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="config">Confluent Schema Registry client config.</param>
        /// <param name="tokenRefreshFunc">A refresh function that can use DI (receives the root <see cref="IServiceProvider"/>).</param>
        /// <param name="configure">Optional configuration of <see cref="SchemaClientOptions"/>.</param>
        /// <param name="enableOpenTelemetryMetrics">If true, registers the OpenTelemetry metrics collector.</param>
        public static IServiceCollection AddSchemaRegistryExtClient(this IServiceCollection services, SchemaRegistryConfig config, Func<IServiceProvider, Task<(string token, DateTime expiresAt)>> tokenRefreshFunc, Action<SchemaClientOptions>? configure = null, bool enableOpenTelemetryMetrics = false)
            => AddSchemaRegistryExtClientInternal(services, config, tokenRefreshFunc: null, tokenRefreshFromServiceProvider: tokenRefreshFunc, configure, enableOpenTelemetryMetrics);

        private static IServiceCollection AddSchemaRegistryExtClientInternal(
            IServiceCollection services,
            SchemaRegistryConfig config,
            Func<Task<(string token, DateTime expiresAt)>>? tokenRefreshFunc,
            Func<IServiceProvider, Task<(string token, DateTime expiresAt)>>? tokenRefreshFromServiceProvider,
            Action<SchemaClientOptions>? configure,
            bool enableOpenTelemetryMetrics)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (tokenRefreshFunc != null && tokenRefreshFromServiceProvider != null)
            {
                throw new ArgumentException("Provide either tokenRefreshFunc or tokenRefreshFromServiceProvider, not both.");
            }

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

            // Register the client factory and token manager (if token refresh function provided)
            services.AddSingleton<ISchemaRegistryClientFactory, DefaultSchemaRegistryClientFactory>();
            if (tokenRefreshFunc != null)
            {
                services.AddSingleton<ITokenManager>(sp => new TokenManager(tokenRefreshFunc, sp.GetService<IMetricsCollector>()));
            }
            else if (tokenRefreshFromServiceProvider != null)
            {
                services.AddSingleton<ITokenManager>(sp => new TokenManager(() => tokenRefreshFromServiceProvider(sp), sp.GetService<IMetricsCollector>()));
            }

            // Register the extended client as the main service (singleton) so it can manage token refresh and registrar reuse
            if (tokenRefreshFunc != null || tokenRefreshFromServiceProvider != null)
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