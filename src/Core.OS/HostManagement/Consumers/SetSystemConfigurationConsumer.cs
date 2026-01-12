using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using HostManagement.Shared.Communication.Enums;
using MassTransit;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

public sealed class SetSystemConfigurationConsumer(IPipeClient pipeClient)
    : IConsumer<SetSystemConfiguration>
{
    public async Task Consume(ConsumeContext<SetSystemConfiguration> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            var result = await pipeClient.SetSystemConfiguration(context.Message.SystemConfiguration, context.CancellationToken);
            if (result?.Status == OperationStatus.Success)
            {
                await context.Publish(new SystemConfigurationChanged(correlationId), context.CancellationToken);
                return;
            }

            await context.Publish(new SetSystemConfigurationError(correlationId, new ErrorInfo((int?)result?.Status ?? -1, result?.Message)),
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            await context.Publish(new SetSystemConfigurationError(correlationId, new ErrorInfo(-1, ex.Message)), context.CancellationToken);
        }
    }
}
