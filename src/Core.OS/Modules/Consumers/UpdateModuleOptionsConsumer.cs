using Core.Shared.Modules;
using Core.Shared.Modules.Commands;
using Core.Shared.Modules.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Modules.Consumers;

public sealed partial class UpdateModuleOptionsConsumer(IModuleOptionsStore optionsStore, ILogger<UpdateModuleOptionsConsumer> logger)
    : IConsumer<UpdateModuleOptions>
{
    public async Task Consume(ConsumeContext<UpdateModuleOptions> context)
    {
        var correlationId = context.Message.CorrelationId;
        var moduleId = context.Message.ModuleId;

        LogConsume(logger, correlationId, moduleId);

        try
        {
            await optionsStore.Store(moduleId, context.Message.Options, context.CancellationToken);

            var changeEvent = new ModuleOptionsChanged(moduleId)
            {
                CorrelationId = correlationId
            };

            LogOptionsUpdated(logger, correlationId, moduleId, context.Message.Options.Count);

            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, correlationId, moduleId);

            // ADR-002: never publish a success-shaped completion for failed work — callers must be able
            // to distinguish a stored option set from a failed store attempt.
            var changeEvent = new ModuleOptionsChanged(moduleId, new ErrorInfo(ModuleErrorCodes.UpdateOptionsFailed, ex.Message))
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume options update correlated by {CorrelationId} for module='{ModuleId}'")]
    private static partial void LogConsume(ILogger<UpdateModuleOptionsConsumer> logger, Guid correlationId, string moduleId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated {OptionsCount} options correlated by {CorrelationId} for module='{ModuleId}'")]
    private static partial void LogOptionsUpdated(ILogger<UpdateModuleOptionsConsumer> logger, Guid correlationId, string moduleId, int optionsCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update options correlated by {CorrelationId} for module='{ModuleId}'")]
    private static partial void LogError(ILogger<UpdateModuleOptionsConsumer> logger, Exception error, Guid correlationId, string moduleId);
}
