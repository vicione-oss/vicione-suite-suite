using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

[ReadOnlyConsumer]
public sealed class ShutdownInstanceConsumer(IHostApplicationLifetime applicationLifetime, ILocalInstanceInformationProvider informationProvider, ILogger<ShutdownInstanceConsumer> logger) : IConsumer<ShutdownInstance>
{
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
    private readonly ILocalInstanceInformationProvider _informationProvider = informationProvider;
    private readonly ILogger<ShutdownInstanceConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<ShutdownInstance> context)
    {
        _logger.LogInformation("Consume {Command} for instance {InstanceId} for reason '{Reason}'",
            nameof(ShutdownInstanceConsumer),
            context.Message.InstanceId,
            context.Message.Reason);

        // prevent blind shutdown
        if (_informationProvider.Local.Id != context.Message.InstanceId)
        {
            _logger.LogInformation("Shutdown skipped because of wrong instance");
            return;
        }

        // shutdown on some slave did fail somehow - can the call fail at all?
        await context.Publish(new InstanceShuttingDown(context.Message.InstanceId, DateTimeOffset.UtcNow.Add(context.Message.Delay)),
            context.CancellationToken);

        // wait till the real shutdown is triggered
        await Task.Delay(context.Message.Delay);

        // user requests restart - track/secure/etc...
        _applicationLifetime.StopApplication();

        // here we are done and the bus is already shutting down        
    }
}
