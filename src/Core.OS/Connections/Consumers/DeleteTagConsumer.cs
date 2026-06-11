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
        var tagId = context.Message.TagId;

        LogConsume(logger, correlationId, tagId);

        try
        {
            var tagToDelete = await dbContext.Tags
                .SingleOrDefaultAsync(tag => tag.Id.Equals(context.Message.TagId), context.CancellationToken);
            if (tagToDelete is null)
            {
                LogTagNotFound(logger, correlationId, tagId);

                // ADR-002: publish success-shaped completion when already deleted (idempotent redelivery)
                var alreadyDeletedTag = new Tag { Id = tagId };
                var alreadyDeletedEvent = new TagsChanged(CrudAction.Deleted, [alreadyDeletedTag])
                {
                    CorrelationId = correlationId
                };
                await context.Publish(alreadyDeletedEvent, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            if (await IsTagProtected(context, tagToDelete))
            {
                return;
            }

            dbContext.Tags.Remove(tagToDelete);

            await dbContext.SaveChangesAsync(context.CancellationToken);

            LogDeleted(logger, correlationId, tagId);

            // We publish an event also when SaveChangesAsync() does nothing because some services rely on a response
            var responseEvent = new TagsChanged(CrudAction.Deleted, [tagToDelete])
            {
                CorrelationId = correlationId
            };
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, context.Message.TagId, correlationId);

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

    public async Task<bool> IsTagProtected(ConsumeContext<DeleteTag> context, Tag tagToDelete)
    {
        if (!tagToDelete.Protected || context.Message.DeleteIfProtected)
            return false;

        var correlationId = context.Message.CorrelationId;
        var tagId = context.Message.TagId;

        LogTagIsProtected(logger, correlationId, tagId);

        var errorInfo = new ErrorInfo(TagErrorCodes.DeleteTagFailed,
                     $"Attempting to delete protected tag='{tagId}'. If this was intended set the '{nameof(DeleteTag.DeleteIfProtected)}'-property to true.");
        var errorTag = new Tag { Id = tagId };
        var errorResponse = new TagsChanged(CrudAction.Deleted, [errorTag])
        {
            CorrelationId = correlationId,
            ErrorInfo = errorInfo
        };
        await context.Publish(errorResponse, context.CancellationToken).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(LogLevel.Debug, "Deleting tag='{TagId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<DeleteTagConsumer> logger, Guid correlationId, Guid tagId);

    [LoggerMessage(LogLevel.Information, "Deleted tag='{TagId}' correlated by {CorrelationId}")]
    private static partial void LogDeleted(ILogger<DeleteTagConsumer> logger, Guid correlationId, Guid tagId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete tag='{TagId}' correlated by {CorrelationId} because it was not found.")]
    private static partial void LogTagIsProtected(ILogger<DeleteTagConsumer> logger, Guid correlationId, Guid tagId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete tag='{TagId}' correlated by {CorrelationId} because it was protected.")]
    private static partial void LogTagNotFound(ILogger<DeleteTagConsumer> logger, Guid correlationId, Guid tagId);

    [LoggerMessage(LogLevel.Error, "Unexpected error on deleting tag='{TagId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<DeleteTagConsumer> logger, Exception exception, Guid correlationId, Guid tagId);
}
