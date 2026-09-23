using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Core.OS.Connections.Consumers;

public sealed partial class UpsertTagConsumer(IConnectionDbContext dbContext, ILogger<UpsertTagConsumer> logger) : IConsumer<UpsertTag>
{
    public async Task Consume(ConsumeContext<UpsertTag> context)
    {
        var correlationId = context.Message.CorrelationId;
        var crudAction = CrudAction.Created;
        var tag = context.Message.Tag;

        LogConsume(logger, correlationId, tag.Id);

        try
        {
            var existingTag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == tag.Id, context.CancellationToken);
            if (existingTag is null)
            {
                var tagEntry = dbContext.Tags.Add(tag);
                existingTag = tagEntry.Entity;
            }
            else
            {
                crudAction = CrudAction.Updated;
                existingTag.Text = tag.Text;

                dbContext.Tags.Update(existingTag);
            }

            _ = await dbContext.SaveChangesAsync(context.CancellationToken);

            LogUpserted(logger, correlationId, tag.Id, crudAction);

            // Publishing the existing tag would hand out the reference to the real entity, so a copy is
            // published instead.
            var copy = new Tag
            {
                Text = existingTag.Text,
                Id = existingTag.Id,
                Protected = existingTag.Protected,
            };

            var responseEvent = new TagsChanged(crudAction, [copy])
            {
                CorrelationId = correlationId
            };

            // We publish an event also when SaveChangesAsync() does nothing because some services rely on a response
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e, correlationId, tag.Id);

            var errorInfo = new ErrorInfo(TagErrorCodes.AddOrUpdateTagFailed, e.Message);
            var tagToDelete = new Tag { Id = tag.Id };
            var responseEvent = new TagsChanged(crudAction, [tagToDelete])
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Upserting tag='{TagId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<UpsertTagConsumer> logger, Guid correlationId, Guid tagId);

    [LoggerMessage(LogLevel.Information, "{Action} tag='{TagId}' correlated by {CorrelationId}")]
    private static partial void LogUpserted(ILogger<UpsertTagConsumer> logger, Guid correlationId, Guid tagId, CrudAction action);

    [LoggerMessage(LogLevel.Error, "Unexpected error on upserting tag='{TagId}' correlated by {CorrelationId}")]
    private static partial void LogUnexpectedError(ILogger<UpsertTagConsumer> logger, Exception exception, Guid correlationId, Guid tagId);
}
