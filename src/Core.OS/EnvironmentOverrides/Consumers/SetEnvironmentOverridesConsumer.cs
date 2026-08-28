using Core.Shared.EnvironmentOverrides.Commands;
using Core.Shared.EnvironmentOverrides.Events;
using Core.Shared.HostManagement.Events;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.EnvironmentOverrides.Consumers;

[ReadOnlyConsumer]
public sealed partial class SetEnvironmentOverridesConsumer(IEnvironmentOverridesRepository repository, ILogger<SetEnvironmentOverridesConsumer> logger)
    : IConsumer<SetEnvironmentOverrides>
{
    public async Task Consume(ConsumeContext<SetEnvironmentOverrides> context)
    {
        var correlationId = context.Message.CorrelationId;

        LogConsume(logger, correlationId);

        try
        {
            await repository.Store(context.Message.Overrides, context.CancellationToken);

            await context.Publish(new SystemRestartRequired(correlationId, RestartReason.EnvironmentConfiguration), context.CancellationToken);
            await context.Publish(new EnvironmentOverridesChanged(correlationId), context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId);

            await context.Publish(new SetEnvironmentOverridesError(correlationId, new ErrorInfo(-1, ex.Message)), context.CancellationToken);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Consuming set environment overrides command correlated by {CorrelationId}.")]
    private static partial void LogConsume(ILogger<SetEnvironmentOverridesConsumer> logger, Guid correlationId);

    [LoggerMessage(LogLevel.Error, "An error occurred while setting environment overrides correlated by {CorrelationId}.")]
    private static partial void LogUnexpectedError(ILogger<SetEnvironmentOverridesConsumer> logger, Exception exception, Guid correlationId);
}
