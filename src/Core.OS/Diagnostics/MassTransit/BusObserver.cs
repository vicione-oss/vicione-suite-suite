using System.Diagnostics;
using System.Runtime.CompilerServices;
using MassTransit;

namespace Core.OS.Diagnostics.MassTransit;

/// <summary>
/// Logs the bus lifecycle events.
/// Is intended to provide startup information, as the tracing might not be working during the bus initialization.
/// </summary>
internal sealed class BusObserver(ILogger<BusObserver> logger) : IBusObserver
{
    public void PostCreate(IBus bus)
    {
        using var activity = StartActivity(bus, "post-create");
        logger.LogDebug("{PostCreateName} {BusAddress}", nameof(PostCreate), bus.Address);
    }

    public Task PreStart(IBus bus)
    {
        using var activity = StartActivity(bus, "pre-start");

        logger.LogDebug(nameof(PreStart));
        return Task.CompletedTask;
    }

    public Task PostStart(IBus bus, Task<BusReady> busReady)
    {
        using var activity = StartActivity(bus, "post-start");

        logger.LogDebug(nameof(PostStart));
        return Task.CompletedTask;
    }

    public Task StartFaulted(IBus bus, Exception exception)
    {
        using var activity = StartActivity(bus, "start-faulted");
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        logger.LogError(exception: exception, nameof(StartFaulted));
        return Task.CompletedTask;
    }

    public Task PreStop(IBus bus)
    {
        logger.LogDebug(nameof(PreStop));
        return Task.CompletedTask;
    }

    public Task PostStop(IBus bus)
    {
        logger.LogDebug(nameof(PostStop));
        return Task.CompletedTask;
    }

    public Task StopFaulted(IBus bus, Exception exception)
    {
        using var activity = StartActivity(bus, "stop-faulted");
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        logger.LogError(exception: exception, nameof(StopFaulted));
        return Task.CompletedTask;
    }

    public void CreateFaulted(Exception exception) => logger.LogError(exception: exception, nameof(CreateFaulted));

    private static Activity? StartActivity(IBus bus, string operation, [CallerMemberName] string activityName = "")
    {
        var activity = CoreActivitySource.Source.StartActivity(activityName);

        activity?.SetTag("messaging.operation", operation);
        activity?.SetTag("messaging.bus.type", bus.Topology.GetType().Name);
        activity?.SetTag("messaging.masstransit.bus_address", bus.Address.ToString());
        activity?.SetTag("server.address", bus.Address.Host);

        return activity;
    }
}
