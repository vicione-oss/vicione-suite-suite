using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed partial class DeleteTagConsumer(IConnectionDbContext dbContext, ILogger<DeleteTagConsumer> logger) : IConsumer<DeleteTag>
{
    public async Task Consume(ConsumeContext<DeleteTag> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
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
            var responseEvent = new TagsChanged(CrudAction.Deleted, [tagToDelete])
            {
                CorrelationId = correlationId
            };
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogError(logger, e, context.Message.TagId, correlationId);

            var errorInfo = new ErrorInfo(TagErrorCodes.DeleteTagFailed, e.Message);
            var tagToDelete = new Tag { Id = context.Message.TagId };
            var responseEvent = new TagsChanged(CrudAction.Deleted, [tagToDelete])
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete tag '{tagId}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<DeleteTagConsumer> logger, Exception ex, Guid? tagId, Guid correlationId);
}
