using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class UpsertTagConsumer(IConnectionDbContext dbContext, ILogger<UpsertTagConsumer> logger) : IConsumer<UpsertTag>
{
    public async Task Consume(ConsumeContext<UpsertTag> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} Tag:{Tag} Id:{TagId}",
            nameof(UpsertTag), correlationId, context.Message.Tag, context.Message.Tag.Id);

        var crudAction = CrudAction.Created;
        var existingTag = await dbContext.Tags.FirstOrDefaultAsync(tag => tag.Id == context.Message.Tag.Id);

        if (existingTag is null)
        {
            var tagEntry = dbContext.Tags.Add(context.Message.Tag);
            existingTag = tagEntry.Entity;
        }
        else
        {
            crudAction = CrudAction.Updated;
            existingTag.Text = context.Message.Tag.Text;

            dbContext.Tags.Update(existingTag);
        }

        _ = await dbContext.SaveChangesAsync(context.CancellationToken);

        // if we publish the existing tag it's the reference to the real entity
        // therefore we make a copy to avoid event consumer issues!
        var copy = new Tag
        {
            Text = existingTag.Text,
            Id = existingTag.Id,
            Protected = existingTag.Protected,
        };

        // We publish an event also when SaveChangesAsync() does nothing because some services rely on a response
        await context.Publish(new TagsChanged(correlationId, crudAction, [copy])).ConfigureAwait(false);
    }
}
