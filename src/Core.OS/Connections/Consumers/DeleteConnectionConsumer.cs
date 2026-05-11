using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed partial class DeleteConnectionConsumer(IConnectionDbContext dbContext, ILogger<DeleteConnectionConsumer> logger) :
    IConsumer<DeleteConnection>
{
    public async Task Consume(ConsumeContext<DeleteConnection> context)
    {
        var correlationId = context.Message.CorrelationId;
        var connectionId = context.Message.ConnectionId;

        LogConsume(logger, correlationId, connectionId);

        try
        {
            var connectionToDelete = await dbContext.Connections
                .Include(c => c.Tags)
                .SingleOrDefaultAsync(connection => connection.Id.Equals(connectionId), context.CancellationToken);

            if (connectionToDelete is null)
            {
                LogConnectionNotFound(logger, correlationId, connectionId);

                var errorInfo = new ErrorInfo(ConnectionErrorCodes.DeleteConnectionFailed,
                             $"Could not delete connection with id '{connectionId}'. No connection was found with that id.");
                var errorConn = new Connection { Id = connectionId };
                var errorResponse = new ConnectionChanged(CrudAction.Deleted, errorConn, [], [])
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };
                await context.Publish(errorResponse, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            dbContext.Connections.Remove(connectionToDelete);

            await dbContext.SaveChangesAsync(context.CancellationToken);

            LogDeleted(logger, correlationId, connectionId);

            // We publish events also when SaveChangesAsync() does nothing because some services rely on a response
            var response = new ConnectionChanged(CrudAction.Deleted, connectionToDelete, [], [])
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, context.Message.ConnectionId, correlationId);

            var errorInfo = new ErrorInfo(ConnectionErrorCodes.DeleteConnectionFailed, e.Message);
            var errorConn = new Connection { Id = connectionId };
            var response = new ConnectionChanged(CrudAction.Deleted, errorConn, [], [])
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Deleting connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<DeleteConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(LogLevel.Information, "Deleted connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogDeleted(ILogger<DeleteConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete connection='{ConnectionId}' correlated by {CorrelationId} because it was not found.")]
    private static partial void LogConnectionNotFound(ILogger<DeleteConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(LogLevel.Error, "Unexpected error on deleting connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<DeleteConnectionConsumer> logger, Exception exception, Guid correlationId, Guid connectionId);
}
