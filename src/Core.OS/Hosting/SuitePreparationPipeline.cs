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

    private static void LogUnhandledExceptionEvent(object sender, UnhandledExceptionEventArgs e)
        => Serilog.Log.Error("Unhandled error! {Error}", e.ExceptionObject);
}
