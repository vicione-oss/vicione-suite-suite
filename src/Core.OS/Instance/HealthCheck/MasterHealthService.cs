using System.Timers;
using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Instance.HealthCheck.Events;

namespace Core.OS.Instance.HealthCheck;

public sealed class MasterHealthService(MasterHealthInfo masterHealthInfo, IServiceProvider services, IOptions<InstanceOptions> options) :
    IMasterHealthService, IDisposable
{
    private readonly MasterHealthInfo _masterHealthInfo = masterHealthInfo;
    private readonly IServiceProvider _services = services;
    private readonly InstanceHealthCheckOptions _options = options.Value.HealthChecks ?? new();
    private Guid? _masterId;
    private bool _receivedHealthStatus;
    private System.Timers.Timer? _timer;
    private bool _isInitialized;
    private readonly Lock _lockObject = new();

    public async Task CheckHealthStatus(Guid id, HealthStatus healthStatus)
    {
        await InitMasterId();

        lock (_lockObject)
        {
            if (!_isInitialized)
                Initialize();
        }

        if (id == _masterId)
        {
            _receivedHealthStatus = true;
            await UpdateHealthStatus(true);
        }
    }

    private void Initialize()
    {
        _timer = new((_options.MasterPublishIntervalInSeconds + 5) * 1000);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();

        _isInitialized = true;
    }

    private async Task InitMasterId()
    {
        if (_masterId is not null)
            return;

        var scope = _services.CreateAsyncScope();

        var instanceProvider = scope.ServiceProvider.GetRequiredService<IInstanceInformationProvider>();
        if (instanceProvider.Local.Type == InstanceType.Master)
        {
            _masterId = instanceProvider.Local.Id;
            await UpdateHealthStatus(true);
            return;
        }

        // can be null if the instance is not yet in the table
        _masterId = (await instanceProvider.GetInstancesInCluster(CancellationToken.None))
            .FirstOrDefault(i => i.Type == InstanceType.Master)?.Id;
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await UpdateHealthStatus(_receivedHealthStatus);

        _receivedHealthStatus = false;
    }

    public void Dispose()
    {
        if (_timer is not null)
        {
            _timer.Elapsed -= OnTimerElapsed;
            _timer.Stop();
            _timer.Dispose();
        }
    }

    private async Task UpdateHealthStatus(bool healthy)
    {
        if (_masterHealthInfo.IsMasterReachable != healthy)
        {
            _masterHealthInfo.IsMasterReachable = healthy;

            await using var scope = _services.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();

            await mediator.Publish(new MasterHealthInfoChanged(_masterHealthInfo.IsMasterReachable));
        }
    }
}
