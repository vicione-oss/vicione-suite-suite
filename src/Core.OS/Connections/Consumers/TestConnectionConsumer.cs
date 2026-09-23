using Core.Shared.Connections.Commands;
using Core.Shared.Connections.Contracts;
using Core.Shared.Connections.Events;
using MassTransit;
using Sdk.Connections.Contracts;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed partial class TestConnectionConsumer(IConnectionTypeRegistry connectionTypeRegistry, ILogger<TestConnectionConsumer> logger) : IConsumer<TestConnection>
{
    public async Task Consume(ConsumeContext<TestConnection> context)
    {
        var correlationId = context.Message.CorrelationId;
        var connection = context.Message.Connection;

        try
        {
            LogConsume(logger, correlationId, connection.Id, connection.Type);

            using CancellationTokenSource timeoutTokenSource = new(10_000);
            using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, context.CancellationToken);

            if (connection.Json is null)
            {
                LogJsonIsEmpty(logger, correlationId, connection.Id);

                await context.Publish(
                    new TestConnectionDoneEvent(correlationId, new TestConnectionResult { ErrorInfo = new ErrorInfo(500, "Connection JSON is null") }),
                    context.CancellationToken);
                return;
            }

            if (!connectionTypeRegistry.TryGetConnectionSerializer(connection.Type, out var serializer)
                || !connectionTypeRegistry.TryGetConnectionTest(connection.Type, out var test))
            {
                LogSerializerIsMissing(logger, correlationId, connection.Id, connection.Type.Name);

                await context.Publish(
                    new TestConnectionDoneEvent(correlationId, new TestConnectionResult { ErrorInfo = new ErrorInfo(500, "No serializer or test registered for connection type") }),
                    context.CancellationToken);
                return;
            }

            var innerConnection = serializer.Deserialize(connection.Json);
            if (innerConnection is null)
            {
                LogFailedToDeserialize(logger, correlationId, connection.Id, connection.Type.Name);

                await context.Publish(
                    new TestConnectionDoneEvent(correlationId, new TestConnectionResult { ErrorInfo = new ErrorInfo(500, "Failed to deserialize connection") }),
                    context.CancellationToken);
                return;
            }

            var result = await test.Test(innerConnection, combinedTokenSource.Token);

            LogConnectionTested(logger, correlationId, connection.Id, connection.Type.Name, result.Success);

            await context.Publish(new TestConnectionDoneEvent(correlationId, new() { ConnectionId = connection.Id, ErrorInfo = result.ErrorInfo, }), context.CancellationToken);
        }
        // Keeps the test from repeating through the error queue.
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, connection.Id);

            await context.Publish(
                new TestConnectionDoneEvent(correlationId, new TestConnectionResult { ErrorInfo = new ErrorInfo(110, e.Message) }),
                context.CancellationToken);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Testing connection='{ConnectionId}' type='{TypeName}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<TestConnectionConsumer> logger, Guid correlationId, Guid connectionId, string? typeName);

    [LoggerMessage(LogLevel.Debug, "Tested connection='{ConnectionId}' type='{TypeName}' correlated by {CorrelationId} with success='{Success}'")]
    private static partial void LogConnectionTested(ILogger<TestConnectionConsumer> logger, Guid correlationId, Guid connectionId, string? typeName, bool success);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to test connection='{ConnectionId}' correlated by {CorrelationId} because json is empty.")]
    private static partial void LogJsonIsEmpty(ILogger<TestConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to test connection='{ConnectionId}' correlated by {CorrelationId} because serializer is missing for type='{TypeName}'.")]
    private static partial void LogSerializerIsMissing(ILogger<TestConnectionConsumer> logger, Guid correlationId, Guid connectionId, string? typeName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to test connection='{ConnectionId}' correlated by {CorrelationId} because can't deserialize type='{TypeName}'.")]
    private static partial void LogFailedToDeserialize(ILogger<TestConnectionConsumer> logger, Guid correlationId, Guid connectionId, string? typeName);

    [LoggerMessage(LogLevel.Error, "Unexpected error on deleting connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<TestConnectionConsumer> logger, Exception exception, Guid correlationId, Guid connectionId);
}
