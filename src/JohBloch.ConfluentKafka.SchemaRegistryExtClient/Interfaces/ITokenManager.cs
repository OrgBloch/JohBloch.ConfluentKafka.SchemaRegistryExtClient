namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces
{
    /// <summary>
    /// Interface for token management (obtaining and refreshing tokens) using async APIs.
    /// </summary>
    public interface ITokenManager
    {
        /// <summary>
        /// Returns a valid access token (refreshes if necessary).
        /// </summary>
        /// <returns>Access token as string, or null if unavailable.</returns>
        Task<string?> GetTokenAsync();

        /// <summary>
        /// Force an immediate token refresh.
        /// </summary>
        Task ForceRefreshAsync();
    }
}
