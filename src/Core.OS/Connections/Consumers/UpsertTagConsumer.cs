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

        try
        {
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

            var responseEvent = new TagsChanged(crudAction, [copy])
            {
                CorrelationId = correlationId
            };

            // We publish an event also when SaveChangesAsync() does nothing because some services rely on a response
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogError(logger, e, context.Message.Tag.Id, correlationId);

            var errorInfo = new ErrorInfo(TagErrorCodes.AddOrUpdateTagFailed, e.Message);
            var tagToDelete = new Tag { Id = context.Message.Tag.Id };
            var responseEvent = new TagsChanged(crudAction, [tagToDelete])
            {
                CorrelationId = correlationId,
                ErrorInfo = errorInfo
            };
            await context.Publish(responseEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to upsert tag '{tagId}' correlated by '{correlationId}'.")]
    private static partial void LogError(ILogger<UpsertTagConsumer> logger, Exception ex, Guid? tagId, Guid correlationId);
}
