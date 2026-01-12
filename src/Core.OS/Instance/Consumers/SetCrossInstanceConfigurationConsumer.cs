using Core.OS.DbContext;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed class SetCrossInstanceConfigurationConsumer(IApplicationDbContext dbContext, ILogger<SetCrossInstanceConfigurationConsumer> logger)
    : IConsumer<SetCrossInstanceConfiguration>
{
    public async Task Consume(ConsumeContext<SetCrossInstanceConfiguration> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;
        var crossInstanceConfiguration = await dbContext.CrossInstanceConfiguration.FirstOrDefaultAsync(context.CancellationToken);

        try
        {
            logger.LogInformation("Consuming {Command} with CorrelationId '{Id}'", nameof(SetCrossInstanceConfiguration), correlationId);

            if (crossInstanceConfiguration is null)
            {
                crossInstanceConfiguration = new CrossInstanceConfiguration();
                dbContext.CrossInstanceConfiguration.Add(crossInstanceConfiguration);
            }
            else
            {
                dbContext.CrossInstanceConfiguration.Update(crossInstanceConfiguration);
            }

            if (context.Message.CultureName is not null)
                crossInstanceConfiguration.CultureName = context.Message.CultureName;

            if (context.Message.TimeZoneId is not null)
                crossInstanceConfiguration.TimeZoneId = context.Message.TimeZoneId;

            await dbContext.Instance.SaveChangesAsync(context.CancellationToken);

            await context.Publish(new CrossInstanceConfigurationChanged(correlationId, crossInstanceConfiguration)).ConfigureAwait(false);
        }
        catch (DbUpdateException e)
        {
            await context.Publish(new CrossInstanceConfigurationError(correlationId, new ErrorInfo(CrossInstanceConfigurationError.AddOrUpdateFailed, e.Message), crossInstanceConfiguration?.Id));
        }
    }
}
