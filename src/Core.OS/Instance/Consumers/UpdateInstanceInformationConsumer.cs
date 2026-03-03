using Core.OS.DbContext;
using Core.OS.Instance.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Core.OS.Instance.Consumers;

public sealed class UpdateInstanceInformationConsumer(
    IApplicationDbContext dbContext,
    InMemoryClusterInformationProvider informationProvider,
    ILocalInstanceInformationProvider localInstanceInformationProvider,
    ILogger<UpdateInstanceInformationConsumer> logger) : IConsumer<UpdateInstanceInformation>
{
    public async Task Consume(ConsumeContext<UpdateInstanceInformation> context)
    {
        var updateInfo = context.Message.InstanceInformation;
        var correlationId = context.CorrelationId ?? Guid.Empty;

        logger.LogDebug("Consume {Command} CorrelationId:{CorrelationId} instance:{InstanceId}",
            nameof(UpdateInstanceInformationConsumer), correlationId, updateInfo.Id);

        var existingInfo = await dbContext.InstanceInfo
            .FirstOrDefaultAsync(info => info.Id == updateInfo.Id);

        if (existingInfo is null)
        {
            await context.Publish(new InstanceInformationUpdated(correlationId, updateInfo, false));
            return;
        }

        // update only properties with public setter
        existingInfo.Name = updateInfo.Name;
        existingInfo.FormattedName = updateInfo.FormattedName;
        existingInfo.Description = updateInfo.Description;

        dbContext.InstanceInfo.Update(existingInfo);

        try
        {
            if (await dbContext.SaveChangesAsync(context.CancellationToken) > 0)
            {
                // update memory instances
                informationProvider.UpdateInstanceInformation(existingInfo);
                localInstanceInformationProvider.UpdateLocal(existingInfo);

                await context.Publish(new InstanceInformationUpdated(correlationId, existingInfo, true)).ConfigureAwait(false);
            }
        }
        catch (DbUpdateException e)
        {
            logger.LogError(e, "Failed to update instance information {Id}", existingInfo.Id);

            await context.Publish(new InstanceInformationUpdated(correlationId, existingInfo, false));
        }
    }
}
