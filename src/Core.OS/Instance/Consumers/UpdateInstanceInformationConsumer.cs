using Core.OS.DbContext;
using Core.OS.Instance.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class UpdateInstanceInformationConsumer(
    IApplicationDbContext dbContext,
    InMemoryClusterInformationProvider informationProvider,
    ILocalInstanceInformationProvider localInstanceInformationProvider,
    ILogger<UpdateInstanceInformationConsumer> logger) : IConsumer<UpdateInstanceInformation>
{
    public async Task Consume(ConsumeContext<UpdateInstanceInformation> context)
    {
        var correlationId = context.Message.CorrelationId;
        var updateInfo = context.Message.InstanceInformation;

        LogConsume(logger, correlationId, updateInfo.Id);

        var existingInfo = await dbContext.InstanceInfo
            .FirstOrDefaultAsync(info => info.Id == updateInfo.Id);

        if (existingInfo is null)
        {
            var notFoundEvent = new InstanceInformationUpdated(correlationId, updateInfo, new ErrorInfo(120, $"Instance='{updateInfo.Id}' not found in database."))
            {
                CorrelationId = correlationId
            };

            await context.Publish(notFoundEvent, context.CancellationToken);
            return;
        }

        // Only properties with a public setter are updated.
        existingInfo.Name = updateInfo.Name;
        existingInfo.FormattedName = updateInfo.FormattedName;
        existingInfo.Description = updateInfo.Description;

        try
        {
            dbContext.InstanceInfo.Update(existingInfo);

            if (await dbContext.SaveChangesAsync(context.CancellationToken) <= 0)
            {
                LogInstanceNotSaved(logger, correlationId, updateInfo.Id);

                var nothingSavedEvent = new InstanceInformationUpdated(correlationId, existingInfo, new ErrorInfo(100, "No item was updated in database."))
                {
                    CorrelationId = correlationId
                };

                await context.Publish(nothingSavedEvent, context.CancellationToken);
                return;
            }

            LogInstanceUpdated(logger, correlationId, updateInfo.Id);

            // Updates the in-memory instances.
            informationProvider.UpdateInstanceInformation(existingInfo);
            localInstanceInformationProvider.UpdateLocal(existingInfo);

            var changeEvent = new InstanceInformationUpdated(correlationId, existingInfo)
            {
                CorrelationId = correlationId
            };

            await context.Publish(changeEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException dbe)
        {
            LogDatabaseError(logger, dbe, correlationId, updateInfo.Id);

            var errorEvent = new InstanceInformationUpdated(correlationId, existingInfo, new ErrorInfo(110, dbe.Message))
            {
                CorrelationId = correlationId
            };

            await context.Publish(errorEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            var errorEvent = new InstanceInformationUpdated(correlationId, existingInfo, new ErrorInfo(100, e.Message))
            {
                CorrelationId = correlationId
            };

            await context.Publish(errorEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume update instance information='{InstanceId}' correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<UpdateInstanceInformationConsumer> logger, Guid correlationId, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated instance information='{InstanceId}' correlated by {CorrelationId}")]
    private static partial void LogInstanceUpdated(ILogger<UpdateInstanceInformationConsumer> logger, Guid correlationId, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Updated instance information='{InstanceId}' correlated by {CorrelationId} updated 0 items")]
    private static partial void LogInstanceNotSaved(ILogger<UpdateInstanceInformationConsumer> logger, Guid correlationId, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update instance information='{InstanceId}' correlated by {CorrelationId}")]
    private static partial void LogDatabaseError(ILogger<UpdateInstanceInformationConsumer> logger, Exception exception, Guid correlationId, Guid instanceId);
}
