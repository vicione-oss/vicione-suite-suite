using Core.OS.Hosting.Contracts;

namespace Core.OS.Hosting;

/// <summary>
/// Builds and executes an ordered sequence of filesystem/workspace preparation steps.
/// Execution stops at the first <see cref="IPreparationAbortResult"/>.
/// </summary>
internal sealed class SuitePreparationPipeline(SuitePreparationContext preparationContext)
    : PreparationPipeline<SuitePreparationPipeline, SuitePreparationContext>(context: preparationContext)
{
    private record PreparationCancelledResult(string Reason) : IPreparationAbortResult;

    private record PreparationFaultedResult(string Reason) : IPreparationAbortResult;

    protected override ILogger Logger => Context.Logger;

    protected override IPreparationAbortResult CreateFaultResult(string stepName, Exception exception)
        => new PreparationFaultedResult($"Preparation step '{stepName}' failed: {exception.Message}");

    /// <summary>
    /// Executes the pipeline while listening for process termination signals (Ctrl+C,
    /// <see cref="AppDomain.ProcessExit"/>). Either signal cancels the pipeline cooperatively.
    /// The signal handlers are always removed before this method returns to avoid
    /// interference with <c>IHostApplicationLifetime</c>.
    /// An <see cref="OperationCanceledException"/> raised by any step is caught and
    /// returned as an <see cref="IPreparationAbortResult"/> rather than propagated.
    /// </summary>
    public async Task<IPreparationResult> RunWithProcessSignalsAsync()
    {
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += OnConsoleCancel;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        // Unlike the two signal handlers above, this one is deliberately left attached: it stays the
        // process-wide crash logger after preparation has finished. It therefore has to flush the
        // static logger itself - see LogUnhandledExceptionEvent.
        AppDomain.CurrentDomain.UnhandledException -= LogUnhandledExceptionEvent;
        AppDomain.CurrentDomain.UnhandledException += LogUnhandledExceptionEvent;

        try
        {
            return await RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return new PreparationCancelledResult("Startup preparation was cancelled by process exit");
        }
        finally
        {
            Console.CancelKeyPress -= OnConsoleCancel;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }

        void OnProcessExit(object? o, EventArgs e) => cts.Cancel();
        void OnConsoleCancel(object? o, ConsoleCancelEventArgs e) { e.Cancel = true; cts.Cancel(); }
    }

    internal static void LogUnhandledExceptionEvent(object sender, UnhandledExceptionEventArgs e)
    {
        Serilog.Log.Error("Unhandled error! {Error}", e.ExceptionObject);

        // When the process dies the runtime terminates it as soon as this handler returns, so neither a
        // finally block nor AppDomain.ProcessExit runs afterwards, so this is the last chance to export
        // the crash to a batching sink (e.g. OpenTelemetry). Closing the logger while the process keeps
        // running would leave the device without any logging at all for the rest of its uptime.
        if (e.IsTerminating)
            Serilog.Log.CloseAndFlush();
    }
}
