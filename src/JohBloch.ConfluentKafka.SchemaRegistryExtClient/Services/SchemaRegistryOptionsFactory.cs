namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services
{
    /// <summary>
    /// Factory/helper for creating SchemaClientOptions from configuration (e.g. appsettings/localsettings).
    /// </summary>
    public static class SchemaRegistryOptionsFactory
    {
        /// <summary>
        /// Creates SchemaClientOptions from IConfiguration (e.g. bound from localsettings/appsettings).
        /// </summary>
        public static SchemaClientOptions CreateOptions(IConfiguration configuration, string sectionName = "SchemaRegistry")
        {
            var config = new JohBloch.ConfluentKafka.SchemaRegistryClient.Models.SchemaRegistryConfiguration();
            configuration.GetSection(sectionName).Bind(config);
            var options = new SchemaClientOptions();
            // Map other options as needed (e.g. cache options, logger, etc.)
            // The config object should be passed directly to the client, not as a property of SchemaClientOptions
            return options;
        }
    }
}
