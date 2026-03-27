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

public sealed partial class UpsertConnectionConsumer(IConnectionDbContext dbContext, ILogger<UpsertConnectionConsumer> logger) : IConsumer<UpsertConnection>
{
    public async Task Consume(ConsumeContext<UpsertConnection> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;
        var action = CrudAction.Created;

        try
        {
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

            await dbContext.SaveChangesAsync(context.CancellationToken);

            // We publish events also when SaveChangesAsync() does nothing because some services rely on a response
            var createdTagsEvent = new TagsChanged(CrudAction.Created, addedTags) { CorrelationId = correlationId };
            await context.Publish(createdTagsEvent, context.CancellationToken).ConfigureAwait(false);

            var updatedTagsEvent = new TagsChanged(CrudAction.Updated, changedTags) { CorrelationId = correlationId };
            await context.Publish(updatedTagsEvent, context.CancellationToken).ConfigureAwait(false);

            var changedEvent = new ConnectionChanged(action, existingConn, addedTags, removedTags) { CorrelationId = correlationId };
            await context.Publish(changedEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e)
        {
            LogError(logger, e, context.Message.Connection.Id, correlationId);

            var errorInfo = new ErrorInfo(ConnectionErrorCodes.AddOrUpdateConnectionFailed, e.Message);
            var responseEvent = new ConnectionChanged(action, context.Message.Connection, [], [])
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to upsert connection '{connectionId}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<UpsertConnectionConsumer> logger, Exception ex, Guid? connectionId, Guid correlationId);
}
