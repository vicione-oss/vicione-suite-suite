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

        try
        {
            var connectionToDelete = await dbContext.Connections
            .Include(c => c.Tags)
            .SingleOrDefaultAsync(connection => connection.Id.Equals(context.Message.ConnectionId), context.CancellationToken);
            if (connectionToDelete is null)
                return;

            dbContext.Connections.Remove(connectionToDelete);

            await dbContext.SaveChangesAsync(context.CancellationToken);

            // We publish events also when SaveChangesAsync() does nothing because some services rely on a response
            var response = new ConnectionChanged(CrudAction.Deleted, connectionToDelete, [], [])
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogError(logger, e, context.Message.ConnectionId, correlationId);

            var errorInfo = new ErrorInfo(ConnectionErrorCodes.DeleteConnectionFailed, e.Message);
            var errorConn = new Connection { Id = context.Message.ConnectionId };
            var response = new ConnectionChanged(CrudAction.Deleted, errorConn, [], [])
            {
                CorrelationId = correlationId
            };

            await context.Publish(response, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete connection '{connectionId}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<DeleteConnectionConsumer> logger, Exception ex, Guid connectionId, Guid correlationId);
}
