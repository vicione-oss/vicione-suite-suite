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
        var correlationId = context.Message.CorrelationId;
        var connection = context.Message.Connection;
        var action = CrudAction.Created;

        LogConsume(logger, correlationId, connection.Id);

        try
        {
            var existingConnection = await dbContext.Connections
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(conn => conn.Id == connection.Id);

            var addedTags = new List<Tag>();
            var changedTags = new List<Tag>();
            var removedTags = new List<Tag>();

            if (existingConnection is null)
            {
                connection.Tags = await dbContext.ProcessTags(connection, addedTags, changedTags, context.CancellationToken);

                existingConnection = dbContext.Connections.Add(connection).Entity;
            }
            else
            {
                removedTags = [.. existingConnection.Tags.Except(connection.Tags)];

                action = CrudAction.Updated;
                existingConnection.Assign(connection);
                existingConnection.Tags = await dbContext.ProcessTags(connection, addedTags, changedTags, context.CancellationToken);

                dbContext.Connections.Update(existingConnection);
            }

            // Add the new tags
            foreach (var addedTag in addedTags)
                dbContext.Tags.Add(addedTag);

            await dbContext.SaveChangesAsync(context.CancellationToken);

            LogUpserted(logger, correlationId, connection.Id, action);

            // We publish events also when SaveChangesAsync() does nothing because some services rely on a response
            var createdTagsEvent = new TagsChanged(CrudAction.Created, addedTags) { CorrelationId = correlationId };
            await context.Publish(createdTagsEvent, context.CancellationToken).ConfigureAwait(false);

            var updatedTagsEvent = new TagsChanged(CrudAction.Updated, changedTags) { CorrelationId = correlationId };
            await context.Publish(updatedTagsEvent, context.CancellationToken).ConfigureAwait(false);

            var changedEvent = new ConnectionChanged(action, existingConnection, addedTags, removedTags) { CorrelationId = correlationId };
            await context.Publish(changedEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e)
        {
            LogUnexpectedError(logger, e, correlationId, connection.Id);

            var errorInfo = new ErrorInfo(ConnectionErrorCodes.AddOrUpdateConnectionFailed, e.Message);
            var responseEvent = new ConnectionChanged(action, connection, [], [])
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Upserting connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<UpsertConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(LogLevel.Information, "{Action} connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogUpserted(ILogger<UpsertConnectionConsumer> logger, Guid correlationId, Guid connectionId, CrudAction action);

    [LoggerMessage(LogLevel.Error, "Unexpected error on upserting connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<UpsertConnectionConsumer> logger, Exception exception, Guid correlationId, Guid connectionId);
}
