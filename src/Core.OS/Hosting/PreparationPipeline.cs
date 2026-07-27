using System.Diagnostics;
using System.Runtime.CompilerServices;
using Core.OS.Hosting.Contracts;

namespace Core.OS.Hosting;

internal static class PreparationResult
{
    public static IPreparationSuccessResult Success { get; } = new PreparationSuccessResult();
}

/// <summary>
/// Base for ordered preparation pipelines executing a sequence of steps over a shared
/// <typeparamref name="TContext"/>. Execution stops at the first <see cref="IPreparationAbortResult"/>.
/// <para>
/// Uses the curiously recurring template pattern (<typeparamref name="TSelf"/>) so the fluent
/// <c>Use</c> overloads return the concrete pipeline type, keeping extension-method chaining intact.
/// </para>
/// </summary>
internal abstract partial class PreparationPipeline<TSelf, TContext>(TContext context)
    where TSelf : PreparationPipeline<TSelf, TContext>
{
    private readonly List<(string Name, Func<TContext, CancellationToken, Task<IPreparationResult>> Step)> _steps = [];

    /// <summary>The shared context passed to every step.</summary>
    protected TContext Context => context;

    /// <summary>Logger used to report per-step execution durations.</summary>
    protected abstract ILogger Logger { get; }

    /// <summary>Adds a step that returns an explicit preparation result.</summary>
    public TSelf Use(Func<TContext, CancellationToken, Task<IPreparationResult>> step, [CallerMemberName] string stepName = "")
    {
        _steps.Add((stepName, step));
        return (TSelf)this;
    }

    /// <summary>Adds an async side-effect step that is always treated as success.</summary>
    public TSelf Use(Func<TContext, CancellationToken, Task> step, [CallerMemberName] string stepName = "")
        => Use(async (ctx, ct) =>
        {
            await step(ctx, ct);
            return PreparationResult.Success;
        }, stepName);

    /// <summary>Adds a synchronous side-effect step that is always treated as success.</summary>
    public TSelf Use(Action step, [CallerMemberName] string stepName = "")
        => Use((_, _) =>
        {
            step();
            return Task.FromResult(PreparationResult.Success);
        }, stepName);

    /// <summary>
    /// Executes all registered steps in order over the shared context.
    /// Returns the first <see cref="IPreparationAbortResult"/> encountered,
    /// or <see cref="PreparationResult.Success"/> if all steps succeed.
    /// <para>
    /// A step that throws (other than <see cref="OperationCanceledException"/>, which is
    /// rethrown to preserve cooperative cancellation) is converted into a pipeline-specific
    /// abort result via <see cref="CreateFaultResult"/> so startup can degrade gracefully
    /// instead of crashing the process.
    /// </para>
    /// </summary>
    public async Task<IPreparationResult> RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (name, step) in _steps)
        {
            var stopwatch = Stopwatch.StartNew();
            IPreparationResult result;

            try
            {
                result = await step(context, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cooperative cancellation is handled by the caller (e.g. process-signal wrapper).
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                LogStepFailed(Logger, ex, name, stopwatch.Elapsed.TotalMilliseconds);
                return CreateFaultResult(name, ex);
            }

            stopwatch.Stop();

            OnStepCompleted(name, stopwatch.Elapsed, result);

            if (result is IPreparationAbortResult)
                return result;
        }

        return PreparationResult.Success;
    }

    /// <summary>
    /// Creates the abort result returned when a step throws an unhandled exception.
    /// Each pipeline supplies its own abort type so the appropriate startup fallback engages.
    /// </summary>
    protected abstract IPreparationAbortResult CreateFaultResult(string stepName, Exception exception);

    /// <summary>
    /// Invoked after each step completes. The default implementation logs the step name and duration.
    /// </summary>
    protected virtual void OnStepCompleted(string stepName, TimeSpan elapsed, IPreparationResult result)
        => LogStepCompleted(Logger, stepName, elapsed.TotalMilliseconds);

    [LoggerMessage(LogLevel.Debug, "Preparation step '{StepName}' completed in {ElapsedMs:F1}ms")]
    private static partial void LogStepCompleted(ILogger logger, string stepName, double elapsedMs);

    [LoggerMessage(LogLevel.Error, "Preparation step '{StepName}' failed after {ElapsedMs:F1}ms")]
    private static partial void LogStepFailed(ILogger logger, Exception ex, string stepName, double elapsedMs);
}
