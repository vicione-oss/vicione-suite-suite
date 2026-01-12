using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace Core.OS.MessageBus.Extensions;

internal static class ConsumeContextExtensions
{
    public static async Task SendToInstance<TCommand>(this ConsumeContext context, TCommand message, Guid instanceId, CancellationToken cancellationToken = default)
        where TCommand : class, IInstanceDependentCommand
    {
        // TODO master health info is not respected here?!
        var endPoint = await context.GetSendEndpoint(MessagingHelper.GetCommandEndpointAddress<TCommand>(instanceId));
        await endPoint.Send(message, cancellationToken);
    }
}
