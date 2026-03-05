using System.Collections.Concurrent;
using MassTransit;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Services;

internal abstract class CompletionSourceHandlerBase<TServiceResult>(IUiMediator uiMediator) : IDisposable
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];
    private volatile bool _disposed;

    protected IUiMediator Mediator { get; private set; } = uiMediator;

    protected void Register<TEvent>(IEventConsumer<TEvent> handler) where TEvent : class, IEvent
        => _subscriptions.Add(Mediator.Register(handler));

    protected bool CompleteWithSuccess(Guid correlationId)
    {
        if (_taskCompletionSourceMap.TryRemove(correlationId, out var taskCompletionSource))
        {
            taskCompletionSource.SetResult(null);
            return true;
        }

        return false;
    }

    protected bool CompleteWithError(Guid correlationId, ErrorInfo? errorInfo)
    {
        if (_taskCompletionSourceMap.TryRemove(correlationId, out var taskCompletionSource))
        {
            taskCompletionSource.SetResult(errorInfo);
            return true;
        }

        return false;
    }

    ~CompletionSourceHandlerBase() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (disposing)
        {
            _subscriptions.Dispose();

            foreach (var (correlationId, taskCompletionSource) in _taskCompletionSourceMap)
            {
                if (_taskCompletionSourceMap.TryRemove(correlationId, out _))
                    taskCompletionSource.TrySetCanceled();
            }
        }
    }

    protected abstract TServiceResult CreateSuccessResult();

    protected abstract TServiceResult CreateErrorResult(string errorMessage, int? errorCode = null);

    private TServiceResult CreateErrorResult(ErrorInfo errorInfo)
        => CreateErrorResult(errorInfo.Message ?? CommonPhrases.AnUnknownErrorOccurred, errorInfo.ErrorCode);

    protected async Task<TServiceResult> SendAndWaitForCompletion<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand, CorrelatedBy<Guid>
        => await SendAndWaitForCompletion(command, CreateErrorResult, null, cancellationToken);

    protected async Task<TServiceResult> SendAndWaitForCompletion<TCommand>(TCommand command, Func<ErrorInfo, TServiceResult> errorOccured, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand, CorrelatedBy<Guid>
        => await SendAndWaitForCompletion(command, errorOccured, null, cancellationToken);

    protected async Task<TServiceResult> SendAndWaitForCompletionAfterwards<TCommand>(TCommand command, Func<CancellationToken, Task>? afterSend = null, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand, CorrelatedBy<Guid>
        => await SendAndWaitForCompletion(command, CreateErrorResult, afterSend, cancellationToken);

    private async Task<TServiceResult> SendAndWaitForCompletion<TCommand>(TCommand command, Func<ErrorInfo, TServiceResult> errorOccured, Func<CancellationToken, Task>? afterSend = null, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand, CorrelatedBy<Guid>
    {
        return await SendAndWaitForCompletionInternal(command.CorrelationId, errorOccured, async ct =>
        {
            await Mediator.Send(command, ct);

            if (afterSend != null)
                await afterSend(ct);

        }, cancellationToken);
    }

    protected async Task<TServiceResult> SendAndWaitForCompletion<TCommand>(TCommand command, Guid instanceId, CancellationToken cancellationToken = default)
        where TCommand : class, IInstanceDependentCommand, CorrelatedBy<Guid>
        => await SendAndWaitForCompletion(command, instanceId, CreateErrorResult, null, cancellationToken);

    protected async Task<TServiceResult> SendAndWaitForCompletion<TCommand>(TCommand command, Guid instanceId, Func<ErrorInfo, TServiceResult> errorOccured, CancellationToken cancellationToken = default)
        where TCommand : class, IInstanceDependentCommand, CorrelatedBy<Guid>
        => await SendAndWaitForCompletion(command, instanceId, errorOccured, null, cancellationToken);

    private async Task<TServiceResult> SendAndWaitForCompletion<TCommand>(TCommand command, Guid instanceId, Func<ErrorInfo, TServiceResult> errorOccured, Func<CancellationToken, Task>? afterSend = null, CancellationToken cancellationToken = default)
        where TCommand : class, IInstanceDependentCommand, CorrelatedBy<Guid>
    {
        return await SendAndWaitForCompletionInternal(command.CorrelationId, errorOccured, async ct =>
        {
            await Mediator.Send(command, instanceId, ct);

            if (afterSend != null)
                await afterSend(ct);

        }, cancellationToken);
    }

    private async Task<TServiceResult> SendAndWaitForCompletionInternal(Guid correlationId, Func<ErrorInfo, TServiceResult> errorOccured, Func<CancellationToken, Task> processCall,
        CancellationToken cancellationToken = default)
    {
        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[correlationId] = taskCompletionSource;

        // Post-add guard: Dispose may have set _disposed and completed its drain
        // between the pre-check and the map insertion above.
        if (_disposed)
        {
            _taskCompletionSourceMap.TryRemove(correlationId, out _);
            taskCompletionSource.TrySetCanceled(cancellationToken);
        }

        try
        {
            if (!_disposed)
            {
                try
                {
                    await processCall(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Ensure the caller receives a proper ISaveResult on failure
                    return CreateErrorResult(new ErrorInfo(0, ex.Message));
                }
            }

            return await WaitForCommandCompletion(taskCompletionSource, errorOccured, cancellationToken);
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(correlationId, out _);
        }
    }

    private async Task<TServiceResult> WaitForCommandCompletion(TaskCompletionSource<ErrorInfo?> taskCompletionSource, Func<ErrorInfo, TServiceResult> errorOccured,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var errorInfo = await taskCompletionSource.Task.WaitAsync(TimeSpan.FromMilliseconds(Constants.CommandTimeoutMs), cancellationToken);
            if (errorInfo is not null)
                return errorOccured.Invoke(errorInfo);

            return CreateSuccessResult();
        }
        catch (OperationCanceledException)
        {
            // WaitAsync throws OperationCanceledException/TaskCanceledException when a task is cancelled
            return CreateSuccessResult();
        }
        catch (TimeoutException)
        {
            return CreateErrorResult(CommonPhrases.TheOperationHasTimedOut);
        }
    }
}
