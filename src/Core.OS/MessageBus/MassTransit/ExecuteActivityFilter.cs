using MassTransit;
using MassTransit.Context;

namespace Core.OS.MessageBus.MassTransit;

public sealed class ExecuteActivityFilter<T>(ILogger<ExecuteActivityFilter<T>> logger) : IFilter<ExecuteContext<T>>
    where T : class
{
    private readonly ILogger _logger = logger;

    public async Task Send(ExecuteContext<T> context, IPipe<ExecuteContext<T>> next)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            // TrackingNumber == CorrelationId
            _logger.LogDebug("ExecuteActivity {Name} with {Args}. TrackingNumber:{TrackingNumber} RequestId:{RequestId} (Attempt:{Attempt})",
                context.ActivityName, context.Arguments.GetType().Name, context.TrackingNumber, context.RequestId, context.GetRetryAttempt());
        }

        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            // MassTransit logs the first activity exception as a warning.
            if (context is RetryExecuteContext<T> { RetryAttempt: >= 1 })
            {
                _logger.LogDebug(ex, "Faulted activity {ActivityName}. Retry...", context.ActivityName);
                throw; // let the calling pipe catch it
            }

            // A failed final retry yields a FaultedActivityResult.
            if (context.Result.IsFaulted(out var error))
            {
                _logger.LogError(error, "Faulted activity {ActivityName}", context.ActivityName);
                return;
            }

            throw; // let the calling pipe catch it
        }
    }

    public void Probe(ProbeContext context) { }
}
