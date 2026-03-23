using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;

namespace Core.OS.Modules.Consumers;

public sealed class UpdateModuleOptionsConsumer(IModuleOptionsStore optionsStore, ILogger<UpdateModuleOptionsConsumer> logger)
    : IConsumer<UpdateModuleOptions>
{
    private readonly IModuleOptionsStore _optionsStore = optionsStore;
    private readonly ILogger<UpdateModuleOptionsConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<UpdateModuleOptions> context)
    {
        _logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} Options.Count:{Count}",
            nameof(UpdateModuleOptions), context.CorrelationId, context.Message.Options.Count);

        try
        {
            await _optionsStore.Store(context.Message.ModuleId, context.Message.Options, context.CancellationToken);

            var changeEvent = new ModuleOptionsChanged(context.Message.ModuleId)
            {
                CorrelationId = context.Message.CorrelationId
            };

            _logger.LogInformation("Stored {Count} options for module '{ModuleId}'",
               context.Message.Options.Count, context.Message.ModuleId);

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store options for module '{ModuleId}'", context.Message.ModuleId);

            var changeEvent = new ModuleOptionsChanged(context.Message.ModuleId)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }
}
