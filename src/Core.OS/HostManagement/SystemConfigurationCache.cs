using System.Net.NetworkInformation;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Core.OS.HostManagement;

public sealed partial class SystemConfigurationCache(IOptions<HostManagementOptions> options, ILogger<SystemConfigurationCache> logger) : IDisposable
{
    private readonly long _cacheLifetimeMs = options.Value.ConfigurationCacheLifetimeMs;
    private Timer? _cacheInvalidationTimer;
    private SystemConfiguration? _cachedConfiguration;
    private Dictionary<string, DHCPLease?>? _cachedDhcpLeases;
    private Dictionary<string, PhysicalAddress?>? _cachedOriginalPhysicalAddresses;
    private List<string>? _cachedNtpFallbackServers;
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

        LogCachedSystemConfiguration(logger);
    }

    public SystemConfiguration? Get()
        => _cachedConfiguration;

    public void SetDhcpLeases(Dictionary<string, DHCPLease?> dhcpLeases)
    {
        lock (_lock)
        {
            _cachedDhcpLeases = dhcpLeases;
        }
    }

    public Dictionary<string, DHCPLease?>? GetDhcpLeases()
        => _cachedDhcpLeases;

    public void SetOriginalPhysicalAddresses(Dictionary<string, PhysicalAddress?> originalPhysicalAddresses)
    {
        lock (_lock)
        {
            _cachedOriginalPhysicalAddresses = originalPhysicalAddresses;
        }
    }

    public Dictionary<string, PhysicalAddress?>? GetOriginalPhysicalAddresses()
        => _cachedOriginalPhysicalAddresses;

    public void SetNtpFallbackServers(List<string> fallbackServers)
    {
        lock (_lock)
        {
            _cachedNtpFallbackServers = fallbackServers;
        }
    }

    public List<string>? GetNtpFallbackServers()
        => _cachedNtpFallbackServers;

    public void Dispose() => _cacheInvalidationTimer?.Dispose();

    public void Invalidate()
    {
        lock (_lock)
        {
            _cachedConfiguration = null;
            _cachedDhcpLeases = null;
            _cachedOriginalPhysicalAddresses = null;
            _cachedNtpFallbackServers = null;
            _cacheInvalidationTimer?.Dispose();
            _cacheInvalidationTimer = null;
        }

        LogCachedSystemConfigurationInvalidated(logger);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cached system configuration")]
    private static partial void LogCachedSystemConfiguration(ILogger<SystemConfigurationCache> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cached system configuration invalidated")]
    private static partial void LogCachedSystemConfigurationInvalidated(ILogger<SystemConfigurationCache> logger);
}
