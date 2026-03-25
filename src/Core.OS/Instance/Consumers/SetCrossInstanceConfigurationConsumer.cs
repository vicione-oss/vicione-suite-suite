using Core.OS.DbContext;
using Core.OS.Modules;
using Core.OS.Modules.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Messaging;

namespace Core.OS.Instance.Consumers;

public sealed partial class SetCrossInstanceConfigurationConsumer(IApplicationDbContext dbContext, IModuleHost moduleHost, ILogger<SetCrossInstanceConfigurationConsumer> logger)
    : IConsumer<SetCrossInstanceConfiguration>
{
    public async Task Consume(ConsumeContext<SetCrossInstanceConfiguration> context)
    {
        var correlationId = context.Message.CorrelationId;

        try
        {
            var crossInstanceConfiguration = await dbContext.CrossInstanceConfiguration.FirstOrDefaultAsync(context.CancellationToken);
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
            {
                crossInstanceConfiguration.CultureName = context.Message.CultureName;

                // if we change it globally we have to provide this to our UI host managing the culture for the Blazor Server,
                // so we set the default request culture to the new value
                moduleHost.SetUiHostCulture(context.Message.CultureName);
            }

            if (context.Message.TimeZoneId is not null)
                crossInstanceConfiguration.TimeZoneId = context.Message.TimeZoneId;

            await dbContext.SaveChangesAsync(context.CancellationToken);

            var changeEvent = new CrossInstanceConfigurationChanged(correlationId, crossInstanceConfiguration);

            await context.Publish(changeEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogConsumeFailed(logger, e);

            var errorInfo = new ErrorInfo(CrossInstanceConfigurationError.AddOrUpdateFailed, e.Message);
            var errorEvent = new CrossInstanceConfigurationError(correlationId, errorInfo, null);

            await context.Publish(errorEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed update cross instance configuration")]
    private static partial void LogConsumeFailed(ILogger<SetCrossInstanceConfigurationConsumer> logger, Exception exception);
}
