using Core.OS.Connections.Extensions;
using Core.OS.DbContext;
using MassTransit;
using MassTransit.Configuration;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class CreateConnectionConsumerDefinition : ConsumerDefinition<CreateConnectionConsumer>
{
    public CreateConnectionConsumerDefinition()
    {
        EndpointDefinition
            = new ConsumerEndpointDefinition<CreateConnectionConsumer>(
                new EndpointSettings<IEndpointDefinition<CreateConnectionConsumer>> { PrefetchCount = 1 });
        ConcurrentMessageLimit = 1;
    }
}

/// <summary>
/// this is used by modules to seed connections - only new connections will be added! 
/// </summary>
public sealed partial class CreateConnectionConsumer(IConnectionDbContext dbContext, ILogger<CreateConnectionConsumer> logger) : IConsumer<CreateConnection>
{
    public async Task Consume(ConsumeContext<CreateConnection> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            var existingConn = await dbContext.Connections
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(conn => conn.Id == context.Message.Connection.Id);

            if (existingConn is not null)
                return;

            var addedTags = new List<Tag>();
            var changedTags = new List<Tag>();

            context.Message.Connection.Tags = await dbContext.ProcessTags(context.Message.Connection,
                addedTags, changedTags, context.CancellationToken);

            existingConn = dbContext.Connections.Add(context.Message.Connection).Entity;

            // add the new tags
            foreach (var addedTag in addedTags)
                dbContext.Tags.Add(addedTag);

            if (await dbContext.SaveChangesAsync(context.CancellationToken) > 0)
            {
                var createdEvent = new TagsChanged(CrudAction.Created, addedTags) { CorrelationId = correlationId };
                await context.Publish(createdEvent, context.CancellationToken).ConfigureAwait(false);

                var connectionEvent = new ConnectionChanged(CrudAction.Created, existingConn, addedTags, changedTags) { CorrelationId = correlationId };
                await context.Publish(connectionEvent, context.CancellationToken).ConfigureAwait(false);

                var updatedEvent = new TagsChanged(CrudAction.Updated, changedTags) { CorrelationId = correlationId };
                await context.Publish(updatedEvent, context.CancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogError(logger, ex, context.Message.Connection.Id, correlationId);
            var errorInfo = new ErrorInfo(ConnectionErrorCodes.AddOrUpdateConnectionFailed, ex.Message);
            var changeEvent = new ConnectionChanged(CrudAction.Created, context.Message.Connection, [], [])
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };
            await context.Publish(changeEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create connection '{connectionId}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<CreateConnectionConsumer> logger, Exception ex, Guid? connectionId, Guid correlationId);
}
