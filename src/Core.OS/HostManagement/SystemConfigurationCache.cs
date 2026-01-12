using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Core.OS.HostManagement;

public sealed class SystemConfigurationCache(IOptions<HostManagementOptions> options, ILogger<SystemConfigurationCache> logger) : IDisposable
{
    private readonly long _cacheLifetimeMs = options.Value.ConfigurationCacheLifetimeMs;
    private Timer? _cacheInvalidationTimer;
    private SystemConfiguration? _cachedConfiguration;
    private readonly Lock _lock = new();

    public void Set(SystemConfiguration config)
    {
        lock (_lock)
        {
            _cachedConfiguration = config;

            _cacheInvalidationTimer?.Dispose();
            _cacheInvalidationTimer = new Timer(_cacheLifetimeMs) { AutoReset = false };
            _cacheInvalidationTimer.Elapsed += (_, _) =>
            {
                Invalidate();
            };
            _cacheInvalidationTimer.Start();
        }

        logger.LogDebug("Cached system configuration");
    }

    public SystemConfiguration? Get()
        => _cachedConfiguration;

    public void Dispose() => _cacheInvalidationTimer?.Dispose();

    public void Invalidate()
    {
        lock (_lock)
        {
            _cachedConfiguration = null;
            _cacheInvalidationTimer?.Dispose();
            _cacheInvalidationTimer = null;
        }

        logger.LogDebug("Cached system configuration invalidated");
    }
}
