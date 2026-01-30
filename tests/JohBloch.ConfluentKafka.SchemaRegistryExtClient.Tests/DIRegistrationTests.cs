using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Confluent.SchemaRegistry;
using Xunit;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services;
using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;

namespace JohBloch.ConfluentKafka.SchemaRegistryExtClient.Tests
{
    using Helpers;

    [LogTestName]
    public class DIRegistrationTests
    {
        [Fact]
        public void Registrar_Is_Same_Instance_When_Resolved_From_DI()
        {
            var services = new ServiceCollection();
            var config = new SchemaRegistryConfig { Url = "http://localhost:8081" };
            services.AddSchemaRegistryExtClient(config);
            var sp = services.BuildServiceProvider();

            var ext = sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>();
            var reg1 = sp.GetRequiredService<ISchemaRegistrar>();
            var reg2 = sp.GetRequiredService<ISchemaRegistrar>();

            Assert.Same(reg1, reg2);
            Assert.Same(reg1, ext.Registrar);
        }

        [Fact]
        public void ExtClient_Is_Registered_As_Interface_Singleton()
        {
            var services = new ServiceCollection();
            var config = new SchemaRegistryConfig { Url = "http://localhost:8081" };
            services.AddSchemaRegistryExtClient(config);
            var sp = services.BuildServiceProvider();

            var iext = sp.GetRequiredService<ISchemaRegistryExtClient>();
            var concrete = sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>();

            Assert.Same(iext, concrete);
        }

        [Fact]
        public void TokenManager_Is_Registered_When_TokenFunc_Provided()
        {
            var services = new ServiceCollection();
            var config = new SchemaRegistryConfig { Url = "http://localhost:8081" };
            services.AddSchemaRegistryExtClient(config, async () => ("t", DateTime.UtcNow.AddMinutes(10)));
            var sp = services.BuildServiceProvider();

            var tm = sp.GetService<ITokenManager>();
            var ext = sp.GetRequiredService<JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient>();

            Assert.NotNull(tm);
            Assert.NotNull(ext.TokenManager);
            Assert.Same(tm, ext.TokenManager);
        }

        [Fact]
        public async Task Client_Recreates_On_Token_Refresh()
        {
            var createCount = 0;
            var factory = new TestFactory(() =>
            {
                createCount++;
                var clientMock = new Moq.Mock<ISchemaRegistryClient>();
                return clientMock.Object;
            });

            var tokenCounter = 0;
            async Task<(string token, DateTime expiresAt)> TokenFunc()
            {
                tokenCounter++;
                return ("t" + tokenCounter, DateTime.UtcNow.AddSeconds(-10)); // force expiry
            }

            var config = new SchemaRegistryConfig { Url = "http://localhost:8081" };
            var client = new JohBloch.ConfluentKafka.SchemaRegistryExtClient.Services.SchemaRegistryExtClient(config, TokenFunc, null, null, factory);

            var c1 = await client.GetClientAsync();
            Assert.Equal(1, createCount);

            // Force refresh and call GetClientAsync again
            await client.TokenManager!.ForceRefreshAsync();
            var c2 = await client.GetClientAsync();
            Assert.Equal(2, createCount);
            Assert.NotSame(c1, c2);
        }
    }
}