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

        LogConsume(logger, correlationId);

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

                // A global change has to reach the UI host, which manages the culture for Blazor Server,
                // so we set the default request culture to the new value
                moduleHost.SetUiHostCulture(context.Message.CultureName);
            }

            if (context.Message.TimeZoneId is not null)
                crossInstanceConfiguration.TimeZoneId = context.Message.TimeZoneId;

            await dbContext.SaveChangesAsync(context.CancellationToken);

            LogConfigurationUpdated(logger, correlationId);

            var changeEvent = new CrossInstanceConfigurationChanged(correlationId, crossInstanceConfiguration);

            await context.Publish(changeEvent, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogConsumeFailed(logger, e, correlationId);

            var errorInfo = new ErrorInfo(CrossInstanceConfigurationError.AddOrUpdateFailed, e.Message);
            var errorEvent = new CrossInstanceConfigurationError(correlationId, errorInfo, null);

            await context.Publish(errorEvent, context.CancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Consume update cross instance configuration correlated by {CorrelationId}")]
    private static partial void LogConsume(ILogger<SetCrossInstanceConfigurationConsumer> logger, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated cross instance configuration correlated by {CorrelationId}")]
    private static partial void LogConfigurationUpdated(ILogger<SetCrossInstanceConfigurationConsumer> logger, Guid correlationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed update cross instance configuration correlated by {CorrelationId}")]
    private static partial void LogConsumeFailed(ILogger<SetCrossInstanceConfigurationConsumer> logger, Exception exception, Guid correlationId);
}
