using Core.OS.Connections.Extensions;
using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

/// <summary>
/// this is used by modules to seed connections - only new connections will be added!
/// </summary>
public sealed partial class CreateConnectionConsumer(IConnectionDbContext dbContext, ILogger<CreateConnectionConsumer> logger) : IConsumer<CreateConnection>
{
    public async Task Consume(ConsumeContext<CreateConnection> context)
    {
        var correlationId = context.Message.CorrelationId;
        var connection = context.Message.Connection;

        LogConsume(logger, correlationId, connection.Id);

        try
        {
            var existingConn = await dbContext.Connections
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(conn => conn.Id == connection.Id);

            if (existingConn is not null)
            {
                LogConnectionAlreadyExists(logger, correlationId, connection.Id);

                var errorInfo = new ErrorInfo(ConnectionErrorCodes.AddOrUpdateConnectionFailed,
                            $"Could not create connection '{connection.Name}'. A connection with the same id already exists.");
                var changeEvent = new ConnectionChanged(CrudAction.Created, connection, [], [])
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(changeEvent, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            var addedTags = new List<Tag>();
            var changedTags = new List<Tag>();

            context.Message.Connection.Tags = await dbContext.ProcessTags(connection,
                addedTags, changedTags, context.CancellationToken);

            existingConn = dbContext.Connections.Add(connection).Entity;

            // The new tags.
            foreach (var addedTag in addedTags)
                dbContext.Tags.Add(addedTag);

            if (await dbContext.SaveChangesAsync(context.CancellationToken) <= 0)
            {
                LogConnectionNotSaved(logger, correlationId, connection.Id);

                var errorInfo = new ErrorInfo(ConnectionErrorCodes.AddOrUpdateConnectionFailed,
                            $"Failed to store connection '{connection.Name}' in database.");
                var changeEvent = new ConnectionChanged(CrudAction.Created, connection, [], [])
                {
                    CorrelationId = correlationId,
                    ErrorInfo = errorInfo
                };

                await context.Publish(changeEvent, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            var createdEvent = new TagsChanged(CrudAction.Created, addedTags) { CorrelationId = correlationId };
            await context.Publish(createdEvent, context.CancellationToken).ConfigureAwait(false);

            var connectionEvent = new ConnectionChanged(CrudAction.Created, existingConn, addedTags, changedTags) { CorrelationId = correlationId };
            await context.Publish(connectionEvent, context.CancellationToken).ConfigureAwait(false);

            var updatedEvent = new TagsChanged(CrudAction.Updated, changedTags) { CorrelationId = correlationId };
            await context.Publish(updatedEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex, correlationId, connection.Id);

            var errorInfo = new ErrorInfo(ConnectionErrorCodes.AddOrUpdateConnectionFailed, ex.Message);
            var changeEvent = new ConnectionChanged(CrudAction.Created, connection, [], [])
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };
            await context.Publish(changeEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Creating connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<CreateConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create connection='{ConnectionId}' correlated by {CorrelationId} because it already exists.")]
    private static partial void LogConnectionAlreadyExists(ILogger<CreateConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed save connection='{ConnectionId}' changes correlated by {CorrelationId} to the database.")]
    private static partial void LogConnectionNotSaved(ILogger<CreateConnectionConsumer> logger, Guid correlationId, Guid connectionId);

    [LoggerMessage(LogLevel.Error, "Unexpected error on creating connection='{ConnectionId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<CreateConnectionConsumer> logger, Exception exception, Guid correlationId, Guid connectionId);
}
