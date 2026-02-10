using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SRTimerFunction.Extensions;

var host = new HostBuilder()
	.ConfigureFunctionsWorkerDefaults()
	.ConfigureServices(services =>
	{
		// Schema Registry configuration is encapsulated in SchemaRegistryServiceCollectionExtensions
		services.AddSchemaRegistryFromEnvironment();
	})
	.Build();

host.Run();
