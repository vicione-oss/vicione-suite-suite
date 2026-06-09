using Core.OS.Hosting.Contracts;

namespace Core.OS.Hosting;

internal static class PreparationResult
{
    public static IPreparationSuccessResult Success { get; } = new PreparationSuccessResult();
}
/// <summary>
/// Builds and executes an ordered sequence of preparation steps.
/// Execution stops at the first <see cref="IPreparationAbortResult"/>.
/// </summary>
internal sealed class SuitePreparationPipeline
{
    private record PreparationCancelledResult(string Reason) : IPreparationAbortResult;

    private readonly List<Func<CancellationToken, Task<IPreparationResult>>> _steps = [];

    /// <summary>Adds a step that returns an explicit preparation result.</summary>
    public SuitePreparationPipeline Use(Func<CancellationToken, Task<IPreparationResult>> step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>Adds a fire-and-forget async step that is always treated as success.</summary>
    public SuitePreparationPipeline Use(Func<CancellationToken, Task> step)
        => Use(async ct => { await step(ct); return PreparationResult.Success; });

    /// <summary>Adds a synchronous side-effect step that is always treated as success.</summary>
    public SuitePreparationPipeline Use(Action step)
        => Use(_ => { step(); return Task.FromResult(PreparationResult.Success); });

    /// <summary>
    /// Executes all registered steps in order.
    /// Returns the first <see cref="IPreparationAbortResult"/> encountered,
    /// or <see cref="PreparationResult.Success"/> if all steps succeed.
    /// </summary>
    public async Task<IPreparationResult> RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var step in _steps)
        {
            var result = await step(cancellationToken);
            if (result is IPreparationAbortResult)
                return result;
        }
        return PreparationResult.Success;
    }

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
}
