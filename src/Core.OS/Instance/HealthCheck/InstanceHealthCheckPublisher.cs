using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Backend.Messaging;

namespace Core.OS.Instance.HealthCheck;

/// <summary>
/// slave service publishing alive events to master and checking the last time it received a command
/// from master to evaluate if it is alive - in case of master health changed a notification is sent to
/// frontend
/// </summary>
internal sealed class InstanceHealthCheckPublisher(ILocalInstanceInformationProvider instanceInfo,
    IServiceProvider services,
    ILogger<InstanceHealthCheckPublisher> logger) : IHealthCheckPublisher
{
    private readonly ILocalInstanceInformationProvider _instanceInfo = instanceInfo;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<InstanceHealthCheckPublisher> _logger = logger;

    private HealthStatus _lastStatus = HealthStatus.Healthy;

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        _logger.LogTrace("HealthStatus {Status} took {Duration}", report.Status, report.TotalDuration);

        using var scope = _services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();
        await mediator.Publish(new InstanceHealthInfo(_instanceInfo.Local.Id, DateTimeOffset.UtcNow, report.Status), cancellationToken);

        if (report.Status != _lastStatus)
            await mediator.Publish(new InstanceHealthChangedEvent(report.Status, _instanceInfo.Local.Id), cancellationToken);

        _lastStatus = report.Status;
    }
}
