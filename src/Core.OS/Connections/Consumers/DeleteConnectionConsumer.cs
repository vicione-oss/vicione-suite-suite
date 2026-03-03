using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class DeleteConnectionConsumer(IConnectionDbContext dbContext, ILogger<DeleteConnectionConsumer> logger) :
    IConsumer<DeleteConnection>
{
    public async Task Consume(ConsumeContext<DeleteConnection> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} ConnectionId:{ConnectionId}",
            nameof(DeleteConnection), correlationId, context.Message.ConnectionId);

        var connectionToDelete = await dbContext.Connections
            .Include(c => c.Tags)
            .SingleOrDefaultAsync(connection => connection.Id.Equals(context.Message.ConnectionId), context.CancellationToken);
        if (connectionToDelete is null)
            return;

        dbContext.Connections.Remove(connectionToDelete);

        try
        {
            await dbContext.SaveChangesAsync(context.CancellationToken);

            // We publish events also when SaveChangesAsync() does nothing because some services rely on a response
            await context.Publish(new ConnectionChanged(correlationId, CrudAction.Deleted, connectionToDelete, [], []))
                .ConfigureAwait(false);
        }
        catch (DbUpdateException e)
        {
            await context.Publish(new ConnectionErrorOccured(correlationId, new ErrorInfo(ConnectionErrorOccured.DeleteConnectionFailed, e.Message),
                connectionToDelete.Id)).ConfigureAwait(false);
        }
    }
}
