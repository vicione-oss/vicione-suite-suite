using Blazor.Shared.Extensions;
using Core.Shared.HostManagement;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.SystemConfiguration.Events;

namespace Blazor.Shared.Services;

public sealed class SystemConfigurationService : ISystemConfigurationService,
    IEventConsumer<SystemConfigurationChanged>,
    IDisposable
{
    private SystemConfiguration? _systemConfiguration;
    private readonly Lock _lock = new();
    private readonly IUiMediator _mediator;
    private readonly ILogger<SystemConfigurationService> _logger;
    private readonly IDisposable? _subscription;
    private DateTimeOffset? _lastDhcpLeaseFetchUtc;

    public DateTimeOffset? LastDhcpLeaseFetchUtc => _lastDhcpLeaseFetchUtc;

    public SystemConfiguration SystemConfiguration
    {
        get
        {
            lock (_lock)
            {
                return _systemConfiguration ?? throw new InvalidOperationException($"Call {nameof(Initialize)} before accesing the configuration");
            }
        }
    }

    public event Func<Task>? SystemConfigurationChanged;

    public SystemConfigurationService(IUiMediator mediator, ILogger<SystemConfigurationService> logger)
    {
        _mediator = mediator;
        _logger = logger;
        _subscription = mediator.Register(this);
    }

    public async Task SetSystemConfiguration(SystemConfiguration newSystemConfiguration)
    {
        var networkInterfaces = newSystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces.Select(k => k.CommonInformation.Name);
        _logger.LogDebug("SetSystemConfiguration with network {Interfaces}", string.Join(", ", networkInterfaces));

        lock (_lock)
        {
            _systemConfiguration ??= new();
            newSystemConfiguration.ApplyTo(_systemConfiguration);
        }

        _lastDhcpLeaseFetchUtc = DateTimeOffset.UtcNow;

        if (SystemConfigurationChanged is not null)
            await SystemConfigurationChanged.Invoke();
    }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        var request = new GetHostMgmtSystemConfiguration();
        try
        {
            var response = await _mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(request, cancellationToken);

            _logger.LogDebug("Initialized system configuration - error:{Error}", response.RequestError is not null);

            if (response.RequestError is not null)
                throw new InvalidOperationException($"Requesting system configuration failed - {response.RequestError.Message}");

            if (response.Configuration is null)
                throw new InvalidOperationException($"Requesting system configuration failed - configuration is not set");

            await SetSystemConfiguration(response.Configuration);
        }
        catch (OperationCanceledException)
        {
            // nothing to do. Will be logged by the mediator
        }
    }

    public void Dispose()
        => _subscription?.Dispose();

    public async Task Consume(ClientContext<SystemConfigurationChanged> context, CancellationToken cancellationToken)
    {
        try
        {
            await Initialize(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System configuration changed - reload failed");
        }
    }
}
