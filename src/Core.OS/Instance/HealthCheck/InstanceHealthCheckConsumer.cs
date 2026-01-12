using Core.Shared.Instance.HealthCheck;
using MassTransit;
using Sdk.Messaging;

namespace Core.OS.Instance.HealthCheck;

[ReadOnlyConsumer]
public class InstanceHealthCheckConsumer(IMasterHealthService masterHealthService) : IConsumer<InstanceHealthInfo>
{
    private readonly IMasterHealthService _masterHealthService = masterHealthService;

    public async Task Consume(ConsumeContext<InstanceHealthInfo> context)
        => await _masterHealthService.CheckHealthStatus(context.Message.SenderInstanceId, context.Message.Status);
}
