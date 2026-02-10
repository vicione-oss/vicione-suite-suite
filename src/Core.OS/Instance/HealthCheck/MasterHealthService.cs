using System.Timers;
using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Instance.HealthCheck.Events;
using Timer = System.Timers.Timer;

namespace Core.OS.Instance.HealthCheck;

public sealed class MasterHealthService(MasterHealthInfo masterHealthInfo, IServiceProvider services, IOptions<InstanceOptions> options, ILogger<MasterHealthService> logger) :
    IMasterHealthService, IDisposable
{
    private readonly InstanceHealthCheckOptions _options = options.Value.HealthChecks ?? new();
    private Guid? _masterId;
    private bool _receivedHealthStatus;
    private Timer? _timer;
    private bool _isInitialized;
    private readonly Lock _lockObject = new();

    public async Task CheckHealthStatus(Guid id, HealthStatus healthStatus, CancellationToken token = default)
    {
        await InitMasterId(token);

        lock (_lockObject)
        {
            if (!_isInitialized)
                Initialize();
        }

        if (id == _masterId)
        {
            _receivedHealthStatus = true;
            await UpdateHealthStatus(true, token);
        }
    }

    private void Initialize()
    {
        _timer = new Timer((_options.MasterPublishIntervalInSeconds + 5) * 1000);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();

        _isInitialized = true;
    }

    private async Task InitMasterId(CancellationToken token)
    {
        if (_masterId is not null)
            return;

        var scope = services.CreateAsyncScope();

        var instanceProvider = scope.ServiceProvider.GetRequiredService<IInstanceInformationProvider>();
        if (instanceProvider.Local.Type == InstanceType.Master)
        {
            _masterId = instanceProvider.Local.Id;
            await UpdateHealthStatus(true, token);
            return;
        }

        // can be null if the instance is not yet in the table
        _masterId = (await instanceProvider.GetInstancesInCluster(token))
            .FirstOrDefault(i => i.Type == InstanceType.Master)?.Id;
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            await UpdateHealthStatus(_receivedHealthStatus, CancellationToken.None);
            _receivedHealthStatus = false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "An error occurred while checking health status.");
        }
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

    private async Task UpdateHealthStatus(bool healthy, CancellationToken token)
    {
        if (masterHealthInfo.IsMasterReachable != healthy)
        {
            masterHealthInfo.IsMasterReachable = healthy;

            await using var scope = services.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();

            await mediator.Publish(new MasterHealthInfoChanged(masterHealthInfo.IsMasterReachable), token);
        }
    }
}
