using Core.OS.Connections.Extensions;
using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Extensions;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class UpsertConnectionConsumer(IConnectionDbContext dbContext, ILogger<UpsertConnectionConsumer> logger) : IConsumer<UpsertConnection>
{
    public async Task Consume(ConsumeContext<UpsertConnection> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} ConnectionId:{ConnectionId}",
            nameof(UpsertConnection), correlationId, context.Message.Connection.Id);

        var action = CrudAction.Created;
        var existingConn = await dbContext.Connections
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(conn => conn.Id == context.Message.Connection.Id);

        var addedTags = new List<Tag>();
        var changedTags = new List<Tag>();
        var removedTags = new List<Tag>();

        if (existingConn is null)
        {
            context.Message.Connection.Tags = await dbContext.ProcessTags(context.Message.Connection,
                addedTags, changedTags, context.CancellationToken);

            existingConn = dbContext.Connections.Add(context.Message.Connection).Entity;
        }
        else
        {
            removedTags = [.. existingConn.Tags.Except(context.Message.Connection.Tags)];

            action = CrudAction.Updated;
            existingConn.Assign(context.Message.Connection);
            existingConn.Tags = await dbContext.ProcessTags(context.Message.Connection, addedTags, changedTags, context.CancellationToken);

            dbContext.Connections.Update(existingConn);
        }

        // Add the new tags
        foreach (var addedTag in addedTags)
            dbContext.Tags.Add(addedTag);

        try
        {
            await dbContext.SaveChangesAsync(context.CancellationToken);

            // We publish events also when SaveChangesAsync() does nothing because some services rely on a response
            await context.Publish(new TagsChanged(correlationId, CrudAction.Created, addedTags)).ConfigureAwait(false);
            await context.Publish(new TagsChanged(correlationId, CrudAction.Updated, changedTags)).ConfigureAwait(false);
            await context.Publish(new ConnectionChanged(correlationId, action, existingConn, addedTags, removedTags)).ConfigureAwait(false);
        }
        catch (DbUpdateException e)
        {
            await context.Publish(new ConnectionErrorOccured(correlationId, new ErrorInfo(ConnectionErrorOccured.AddOrUpdateConnectionFailed, e.Message),
                existingConn.Id)).ConfigureAwait(false);
        }
    }
}
