using MassTransit;
using Sdk.SystemConfiguration.Events;

namespace Core.OS.HostManagement.Consumers;

public sealed class SystemConfigurationChangedConsumer(SystemConfigurationCache cache) : IConsumer<SystemConfigurationChanged>
{
    public Task Consume(ConsumeContext<SystemConfigurationChanged> context)
    {
        cache.Invalidate();
        return Task.CompletedTask;
    }
}
