using JohBloch.ConfluentKafka.SchemaRegistryExtClient.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace SRTimerFunction.Functions;

public sealed class HeartbeatFunction
{
    private readonly ILogger<HeartbeatFunction> _logger;
    private readonly ISchemaRegistryExtClient _schemaRegistry;

    public HeartbeatFunction(ILogger<HeartbeatFunction> logger, ISchemaRegistryExtClient schemaRegistry)
    {
        _logger = logger;
        _schemaRegistry = schemaRegistry;
    }

    [Function(nameof(HeartbeatFunction))]
    public async Task RunAsync([TimerTrigger("0 */5 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Heartbeat running at {UtcNow}", DateTime.UtcNow);

        // Demonstrate that the client can be resolved from DI.
        // (We intentionally don't call Schema Registry in the sample.)
        _ = await _schemaRegistry.GetClientAsync().ConfigureAwait(false);

        _logger.LogInformation("Schema Registry client resolved successfully.");
    }
}
