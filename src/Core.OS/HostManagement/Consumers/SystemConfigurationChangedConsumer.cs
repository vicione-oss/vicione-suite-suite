using MassTransit;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

public sealed partial class SystemConfigurationChangedConsumer(SystemConfigurationCache cache, ILogger<SystemConfigurationChangedConsumer> logger) : IConsumer<SystemConfigurationChanged>
{
    public Task Consume(ConsumeContext<SystemConfigurationChanged> context)
    {
        LogConsume(logger);

        cache.Invalidate();
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Consume system configuration change, invalidating cache.")]
    private static partial void LogConsume(ILogger<SystemConfigurationChangedConsumer> logger);
}
