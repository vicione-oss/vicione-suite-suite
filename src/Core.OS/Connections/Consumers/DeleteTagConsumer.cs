using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed class DeleteTagConsumer(IConnectionDbContext dbContext, ILogger<DeleteTagConsumer> logger) : IConsumer<DeleteTag>
{
    public async Task Consume(ConsumeContext<DeleteTag> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} TagId:{TagId}",
            nameof(DeleteTag), correlationId, context.Message.TagId);

        var tagToDelete = await dbContext.Tags
            .SingleOrDefaultAsync(tag => tag.Id.Equals(context.Message.TagId), context.CancellationToken);
        if (tagToDelete is null)
            return;

        if (tagToDelete.Protected && !context.Message.DeleteIfProtected)
        {
            logger.LogWarning("Attempting to delete protected Tag {Tag}. If this was intended set the '{Force}'-property to true",
                tagToDelete, nameof(DeleteTag.DeleteIfProtected));
            return;
        }

        dbContext.Tags.Remove(tagToDelete);

        await dbContext.SaveChangesAsync(context.CancellationToken);

        // We publish an event also when SaveChangesAsync() does nothing because some services rely on a response
        await context.Publish(new TagsChanged(correlationId, CrudAction.Deleted, [tagToDelete])).ConfigureAwait(false);
    }
}
