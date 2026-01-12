using MassTransit;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

public sealed class SystemConfigurationChangedConsumer(SystemConfigurationCache cache, ILogger<SystemConfigurationChangedConsumer> logger) : IConsumer<SystemConfigurationChanged>
{
    public Task Consume(ConsumeContext<SystemConfigurationChanged> context)
    {
        cache.Invalidate();
        logger.LogDebug("System configuration changed and cache is invalid.");
        return Task.CompletedTask;
    }
}
