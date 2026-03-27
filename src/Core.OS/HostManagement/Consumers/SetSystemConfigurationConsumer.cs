using Core.OS.HostManagement.Extensions;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts;
using MassTransit;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class SetSystemConfigurationConsumer(IPipeClient pipeClient, SystemConfigurationCache cache, ILogger<SetSystemConfigurationConsumer> logger)
    : IConsumer<SetSystemConfiguration>
{
    public async Task Consume(ConsumeContext<SetSystemConfiguration> context)
    {
        var correlationId = context.CorrelationId ?? Guid.Empty;

        try
        {
            // try to get previous config from cache
            var previousConfig = cache.Get();
            if (previousConfig is null)
            {
                // and only query it from HostManagement if not in cache
                var responseConfig = await pipeClient.GetSystemConfiguration(context.CancellationToken);
                if (responseConfig?.Status == OperationStatus.Success && responseConfig.Configuration is not null)
                {
                    previousConfig = responseConfig.Configuration;
                }
            }

            var result = await pipeClient.SetSystemConfiguration(context.Message.SystemConfiguration, context.CancellationToken);
            if (result?.Status == OperationStatus.Success)
            {
                await context.Publish(new SystemConfigurationChanged { CorrelationId = correlationId }, context.CancellationToken);

                if (EvaluateRequireSystemRestart(context.Message.SystemConfiguration, previousConfig))
                {
                    LogSystemConfigurationRequiresRestart(logger);
                    await context.Publish(new SystemRestartRequired(correlationId, RestartReason.SystemConfiguration), context.CancellationToken);
                }
                return;
            }

            await context.Publish(new SetSystemConfigurationError(correlationId, new ErrorInfo((int?)result?.Status ?? -1, result?.Message)),
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogErrorSettingSystemConfiguration(logger, ex);

            await context.Publish(new SetSystemConfigurationError(correlationId, new ErrorInfo(-1, ex.Message)), context.CancellationToken);
        }
    }

    private static bool EvaluateRequireSystemRestart(SystemConfiguration applied, SystemConfiguration? previous)
    {
        if (previous is null)
            return false;

        if (applied.NetworkProxySettings is not null && previous.NetworkProxySettings is not null)
        {
            // Should we evaluate the other proxy settings as well?
            return !applied.NetworkProxySettings.HTTP.Equals(previous.NetworkProxySettings.HTTP)
                || !applied.NetworkProxySettings.HTTPS.Equals(previous.NetworkProxySettings.HTTPS)
                || !applied.NetworkProxySettings.FTP.Equals(previous.NetworkProxySettings.FTP)
                || !applied.NetworkProxySettings.SFTP.Equals(previous.NetworkProxySettings.SFTP);
        }

        return false;
    }

    [LoggerMessage(LogLevel.Information, "System configuration changes requires system restart to apply all settings.")]
    private static partial void LogSystemConfigurationRequiresRestart(ILogger<SetSystemConfigurationConsumer> logger);

    [LoggerMessage(LogLevel.Error, "An error occurred while setting the system configuration.")]
    private static partial void LogErrorSettingSystemConfiguration(ILogger<SetSystemConfigurationConsumer> logger, Exception exception);
}
