using MassTransit;
using MassTransit.Context;

namespace Core.OS.MessageBus.MassTransit;

public sealed partial class ExecuteActivityFilter<T>(ILogger<ExecuteActivityFilter<T>> logger) : IFilter<ExecuteContext<T>>
    where T : class
{
    private readonly ILogger _logger = logger;

    public async Task Send(ExecuteContext<T> context, IPipe<ExecuteContext<T>> next)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            // TrackingNumber == CorrelationId
            var attempt = context.GetRetryAttempt();
            LogExecuteActivity(_logger, context.ActivityName, context.Arguments.GetType().Name, context.TrackingNumber, context.RequestId, attempt);
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
                LogFaultedActivityRetry(_logger, ex, context.ActivityName);
                throw; // let the calling pipe catch it
            }

            // A failed final retry yields a FaultedActivityResult.
            if (context.Result.IsFaulted(out var error))
            {
                LogFaultedActivity(_logger, error, context.ActivityName);
                return;
            }

            throw; // let the calling pipe catch it
        }
    }

    public void Probe(ProbeContext context) { }

    [LoggerMessage(Level = LogLevel.Debug, Message = "ExecuteActivity {Name} with {Args}. TrackingNumber:{TrackingNumber} RequestId:{RequestId} (Attempt:{Attempt})")]
    private static partial void LogExecuteActivity(ILogger logger, string name, string args, Guid trackingNumber, Guid? requestId, int attempt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Faulted activity {ActivityName}. Retry...")]
    private static partial void LogFaultedActivityRetry(ILogger logger, Exception exception, string activityName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Faulted activity {ActivityName}")]
    private static partial void LogFaultedActivity(ILogger logger, Exception exception, string activityName);
}
