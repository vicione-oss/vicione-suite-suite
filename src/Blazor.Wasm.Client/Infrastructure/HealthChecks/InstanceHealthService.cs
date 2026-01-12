using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Client.Infrastructure;

namespace Blazor.Wasm.Client.Infrastructure.HealthChecks;

public sealed class InstanceHealthService : IEventConsumer<InstanceHealthChangedEvent>, IDisposable
{
    private readonly ILogger<InstanceHealthService> _logger;
    private readonly IDisposable _subscriptionHandle;

    public event Func<HealthStatus, Task>? HealthChanged;

    public InstanceHealthService(IUiMediator mediator, ILogger<InstanceHealthService> logger)
    {
        _logger = logger;
        _subscriptionHandle = mediator.Register(this);
    }

    public async Task Consume(ClientContext<InstanceHealthChangedEvent> context, CancellationToken cancellationToken)
    {
        if (HealthChanged is not null)
            await HealthChanged.Invoke(context.Message.Status);

        _logger.LogInformation("Handle {Event} backend status is {Status}", nameof(InstanceHealthChangedEvent), context.Message.Status);
    }

    public void Dispose()
    {
        _subscriptionHandle.Dispose();
    }
}
