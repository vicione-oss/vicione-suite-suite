using System.Diagnostics;
using System.Runtime.CompilerServices;
using MassTransit;

namespace Core.OS.Diagnostics.MassTransit;

/// <summary>
/// Logs the bus lifecycle events.
/// Is intended to provide startup information, as the tracing might not be working during the bus initialization.
/// </summary>
internal sealed partial class BusObserver(ILogger<BusObserver> logger) : IBusObserver
{
    public void PostCreate(IBus bus)
    {
        using var activity = StartActivity(bus, "post-create");
        LogPostCreate(logger, nameof(PostCreate), bus.Address);
    }

    public Task PreStart(IBus bus)
    {
        using var activity = StartActivity(bus, "pre-start");

        LogBusEvent(logger, nameof(PreStart));
        return Task.CompletedTask;
    }

    public Task PostStart(IBus bus, Task<BusReady> busReady)
    {
        using var activity = StartActivity(bus, "post-start");

        LogBusEvent(logger, nameof(PostStart));
        return Task.CompletedTask;
    }

    public Task StartFaulted(IBus bus, Exception exception)
    {
        using var activity = StartActivity(bus, "start-faulted");
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        LogBusEventFaulted(logger, exception, nameof(StartFaulted));
        return Task.CompletedTask;
    }

    public Task PreStop(IBus bus)
    {
        LogBusEvent(logger, nameof(PreStop));
        return Task.CompletedTask;
    }

    public Task PostStop(IBus bus)
    {
        LogBusEvent(logger, nameof(PostStop));
        return Task.CompletedTask;
    }

    public Task StopFaulted(IBus bus, Exception exception)
    {
        using var activity = StartActivity(bus, "stop-faulted");
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

        LogBusEventFaulted(logger, exception, nameof(StopFaulted));
        return Task.CompletedTask;
    }

    public void CreateFaulted(Exception exception) => LogBusEventFaulted(logger, exception, nameof(CreateFaulted));

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bus lifecycle event {BusEvent} {BusAddress}")]
    private static partial void LogPostCreate(ILogger logger, string busEvent, Uri busAddress);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bus lifecycle event {BusEvent}")]
    private static partial void LogBusEvent(ILogger logger, string busEvent);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bus lifecycle event {BusEvent}")]
    private static partial void LogBusEventFaulted(ILogger logger, Exception exception, string busEvent);

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
