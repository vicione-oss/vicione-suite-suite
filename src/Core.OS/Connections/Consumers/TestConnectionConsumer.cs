using Core.Shared.Connections.Commands;
using Core.Shared.Connections.Contracts;
using Core.Shared.Connections.Events;
using MassTransit;
using Sdk.Connections.Contracts;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class TestConnectionConsumer(IConnectionTypeRegistry connectionTypeRegistry, ILogger<TestConnectionConsumer> logger) : IConsumer<TestConnection>
{
    public async Task Consume(ConsumeContext<TestConnection> context)
    {
        try
        {
            logger.LogDebug("Consumed {CommandName} CorrelationId:{CorrelationId} ConnectionId:{ConnectionId}",
                nameof(TestConnection),
                context.CorrelationId,
                context.Message.Connection.Id);

            using CancellationTokenSource timeoutTokenSource = new(10_000);
            using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, context.CancellationToken);

            if (context.Message.Connection.Json is null)
            {
                await context.Publish(
                    new TestConnectionDoneEvent(context.Message.RequestId, new TestConnectionResult { ErrorInfo = new ErrorInfo(500, "Connection JSON is null") }),
                    context.CancellationToken);
                return;
            }

            if (!connectionTypeRegistry.TryGetConnectionSerializer(context.Message.Connection.Type, out var serializer)
                || !connectionTypeRegistry.TryGetConnectionTest(context.Message.Connection.Type, out var test))
            {
                await context.Publish(
                    new TestConnectionDoneEvent(context.Message.RequestId, new TestConnectionResult { ErrorInfo = new ErrorInfo(500, "No serializer or test registered for connection type") }),
                    context.CancellationToken);
                return;
            }

            var connection = serializer.Deserialize(context.Message.Connection.Json);

            if (connection is null)
            {
                await context.Publish(
                    new TestConnectionDoneEvent(context.Message.RequestId, new TestConnectionResult { ErrorInfo = new ErrorInfo(500, "Failed to deserialize connection") }),
                    context.CancellationToken);
                return;
            }

            var result = await test.Test(connection, combinedTokenSource.Token);

            await context.Publish(new TestConnectionDoneEvent(context.Message.RequestId, new() { ConnectionId = context.Message.Connection.Id, ErrorInfo = result.ErrorInfo, }), context.CancellationToken);
        }
        // avoid repeats through error queue for the test
        catch (Exception e)
        {
            await context.Publish(
                new TestConnectionDoneEvent(context.Message.RequestId, new TestConnectionResult { ErrorInfo = new ErrorInfo(110, e.Message) }),
                context.CancellationToken);
        }
    }
}
