using Confluent.SchemaRegistry;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;

namespace SRTimerFunction.Extensions;

public static class SchemaRegistryServiceCollectionExtensions
{
    public static IServiceCollection AddSchemaRegistryFromEnvironment(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        static string Env(string key) => Environment.GetEnvironmentVariable(key)
            ?? throw new InvalidOperationException($"Missing env var: {key}");

        static string? EnvOpt(string key) => Environment.GetEnvironmentVariable(key);

        // Read Schema Registry settings from environment variables
        var config = new SchemaRegistryConfig
        {
            Url = Env("SchemaRegistry:Url")
        };

        // Use bearer token auth against Confluent Schema Registry
        config.BearerAuthCredentialsSource = BearerAuthCredentialsSource.StaticToken;

        var logicalCluster = EnvOpt("SchemaRegistry:LogicalCluster");
        var identityPoolId = EnvOpt("SchemaRegistry:IdentityPoolId");

        // MSAL-based client credentials flow for acquiring access tokens
        var authority = Env("SchemaRegistry:Authority");
        var clientId = Env("SchemaRegistry:ClientId");
        var clientSecret = Env("SchemaRegistry:ClientSecret");
        var scope = Env("SchemaRegistry:Scope");

        var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (scopes.Length == 0)
        {
            throw new InvalidOperationException("SchemaRegistry:Scope must contain at least one scope.");
        }

        var msalApp = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(authority)
            .WithClientSecret(clientSecret)
            .Build();

        Func<Task<(string token, DateTime expiresAt)>> tokenRefreshFunc = async () =>
        {
            var result = await msalApp
                .AcquireTokenForClient(scopes)
                .ExecuteAsync()
                .ConfigureAwait(false);

            return (result.AccessToken, result.ExpiresOn.UtcDateTime);
        };

        services.AddSchemaRegistryExtClient(
            config,
            tokenRefreshFunc,
            opts =>
            {
                // Confluent Cloud OAuth extensions (optional)
                opts.LogicalCluster = logicalCluster;
                opts.IdentityPoolId = identityPoolId;
            });

        return services;
    }
}
