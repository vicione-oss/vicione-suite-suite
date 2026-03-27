using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Connections.Extensions;

internal static class IEventConsumerExtensions
{
    public static Task ConsumeConnectionChanged(this IEventConsumer<ConnectionChanged> eventConsumer,
        Guid correlationId, CrudAction crudAction, Connection connection)
    {
        var connectionChangedEvent = new ConnectionChanged(crudAction, connection, [], []) { CorrelationId = correlationId };
        var context = ClientContextFactory.Create(connectionChangedEvent);

        return eventConsumer.Consume(context, TestContext.Current.CancellationToken);
    }
}
