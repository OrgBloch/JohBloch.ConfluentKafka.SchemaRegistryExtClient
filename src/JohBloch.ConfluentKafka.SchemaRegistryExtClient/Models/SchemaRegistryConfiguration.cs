using System;

namespace JohBloch.ConfluentKafka.SchemaRegistryClient.Models
{
    /// <summary>
    /// Configuration class for Schema Registry client, suitable for binding from appsettings/localsettings.
    /// </summary>
    public class SchemaRegistryConfiguration
    {
        /// <summary>
        /// The base URL of the Schema Registry.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// The API key for authentication (if using API key auth).
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// The API secret for authentication (if using API key auth).
        /// </summary>
        public string? ApiSecret { get; set; }

        /// <summary>
        /// The username for SCRAM or basic auth (optional).
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// The password for SCRAM or basic auth (optional).
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Optional: The Azure Managed Identity client ID (if using MSI auth).
        /// </summary>
        public string? ManagedIdentityClientId { get; set; }

        /// <summary>
        /// Optional: The Azure AD tenant ID (if using AAD auth).
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Optional: The Azure AD client ID (if using AAD auth).
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Optional: The Azure AD client secret (if using AAD auth).
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Optional: The authority/issuer URL for AAD auth.
        /// </summary>
        public string? Authority { get; set; }

        /// <summary>
        /// Optional: Token endpoint URL for OAuth2 flows.
        /// </summary>
        public string? TokenEndpointUrl { get; set; }

        /// <summary>
        /// Optional: OAuth2 scopes (space-separated or comma-separated).
        /// </summary>
        public string? OAuthScopes { get; set; }

        /// <summary>
        /// Optional: OAuth2 audience (if required by the provider).
        /// </summary>
        public string? OAuthAudience { get; set; }

        /// <summary>
        /// Optional: Logical cluster identifier for Confluent Cloud (if required).
        /// </summary>
        public string? LogicalCluster { get; set; }

        /// <summary>
        /// Optional: Identity pool ID for Confluent Cloud (if required).
        /// </summary>
        public string? IdentityPoolId { get; set; }

        /// <summary>
        /// Optional: Scope (alias for OAuthScopes).
        /// </summary>
        public string? Scope { get => OAuthScopes; set => OAuthScopes = value; }
    }
}
