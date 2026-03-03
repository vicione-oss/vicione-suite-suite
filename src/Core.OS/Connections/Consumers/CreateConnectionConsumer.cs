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
public sealed class CreateConnectionConsumer(IConnectionDbContext dbContext, ILogger<CreateConnectionConsumer> logger) : IConsumer<CreateConnection>
{
    public async Task Consume(ConsumeContext<CreateConnection> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} ConnectionId:{ConnectionId}",
            nameof(CreateConnection), correlationId, context.Message.Connection.Id);

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

        try
        {
            if (await dbContext.SaveChangesAsync(context.CancellationToken) > 0)
            {
                await context.Publish(new TagsChanged(correlationId, CrudAction.Created, addedTags)).ConfigureAwait(false);

                await context.Publish(new ConnectionChanged(correlationId, CrudAction.Created, existingConn, addedTags, changedTags))
                    .ConfigureAwait(false);

                await context.Publish(new TagsChanged(correlationId, CrudAction.Updated, changedTags)).ConfigureAwait(false);
            }
        }
        catch (DbUpdateException e)
        {
            await context.Publish(new ConnectionErrorOccured(correlationId,
                new ErrorInfo(ConnectionErrorOccured.AddOrUpdateConnectionFailed, e.Message), existingConn.Id)).ConfigureAwait(false);
        }
    }
}
