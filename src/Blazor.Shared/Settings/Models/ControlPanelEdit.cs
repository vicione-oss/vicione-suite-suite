using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;

namespace Blazor.Shared.Settings.Models;

internal sealed partial class ControlPanelEdit<TControlPanelState>(TControlPanelState controlPanelState, IServiceProvider serviceProvider) : IControlPanelEdit
    where TControlPanelState : class, IControlPanelState
{
    private readonly TControlPanelState _controlPanelState = controlPanelState;

    private readonly ILogger<ControlPanelEdit<TControlPanelState>> _logger =
        serviceProvider.GetRequiredService<ILogger<ControlPanelEdit<TControlPanelState>>>();

    private readonly Lazy<IControlPanelResetHandler<TControlPanelState>?> _resetHandler =
        new(() => serviceProvider.GetService<IControlPanelResetHandler<TControlPanelState>>());

    private readonly Lazy<IControlPanelSaveHandler<TControlPanelState>?> _saveHandler =
        new(() => serviceProvider.GetService<IControlPanelSaveHandler<TControlPanelState>>());

    private readonly Lazy<IControlPanelCancelHandler<TControlPanelState>?> _cancelHandler =
        new(() => serviceProvider.GetService<IControlPanelCancelHandler<TControlPanelState>>());

    private CancellationTokenSource? _handlerMethodInvocationCancellationTokenSource;
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly CancellationTokenSource _semaphoreCancellationTokenSource = new();

    private bool _disposedAsync;
    private bool _isRunning;

    public bool IsRunning
    {
        get => _isRunning;

        private set
        {
            if (_isRunning == value)
                return;

            _isRunning = value;

            Changed?.Invoke(new PropertiesChangedEventArgs(this, new HashSet<string> { nameof(IsRunning) }));
        }
    }

    public event Action<PropertiesChangedEventArgs>? Changed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        // For now, we do not call CancelEdit because it would require an await on the async behavior
        // which could result in memory leaks when the await never returns

        await _semaphoreCancellationTokenSource.CancelAsync();
        _semaphoreCancellationTokenSource.Dispose();

        await _semaphore.WaitAsync();
        try
        {
            if (_handlerMethodInvocationCancellationTokenSource is not null)
            {
                try
                {
                    await _handlerMethodInvocationCancellationTokenSource.CancelAsync();
                }
                catch (Exception e)
                {
                    LogUnexpectedErrorWhileRequestingCancellation(_logger, e);
                }

                _handlerMethodInvocationCancellationTokenSource.Dispose();
            }
        }
        finally
        {
            _semaphore.Release();
        }

        _semaphore.Dispose();
    }

    public async Task<bool> Reset()
    {
        var resetHandler = _resetHandler.Value;
        if (resetHandler is null)
            return true; // handle missing handler as if cancel was successful as otherwise handler should be mandatory

        try
        {
            var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

            LogResetHandlerInvoking(_logger, typeof(TControlPanelState).Name);
            await resetHandler.Reset(_controlPanelState, cancellationToken);
            LogResetHandlerInvoked(_logger, typeof(TControlPanelState).Name);

            IsRunning = false;

            return true;
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully
        }

        return false;
    }

    public void Begin()
        => IsRunning = true;

    public async Task<ISaveResult> Save()
    {
        if (!_isRunning)
            return new SaveErrorResult("Cannot finish an edit that has not yet begun.");

        var saveHandler = _saveHandler.Value;
        if (saveHandler == null)
            return new SaveErrorResult("No save handler found.");

        try
        {
            var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

            LogSaveHandlerInvoking(_logger, typeof(TControlPanelState).Name);
            var saveResult = await saveHandler.Save(_controlPanelState, cancellationToken);
            LogSaveHandlerInvoked(_logger, typeof(TControlPanelState).Name);

            if (saveResult is SaveSuccessResult)
                IsRunning = false;

            return saveResult;
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here.

            return new SaveSuccessResult();
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully

            return new SaveSuccessResult();
        }
        catch (Exception e)
        {
            // We log the exception as warning because obsolete OnSave implementations in control panels
            // rely on exception flow to transport errors like validation errors to the UI.
            _logger.LogWarning(e, "Exception was thrown");

            return new SaveErrorResult(e.Message);
        }
    }

    public async Task<bool> Cancel()
    {
        if (!_isRunning)
            return false;

        var cancelHandler = _cancelHandler.Value;
        if (cancelHandler is null)
            return true; // handle missing handler as if cancel was successful as otherwise handler should be mandatory

        try
        {
            var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

            LogCancelHandlerInvoking(_logger, typeof(TControlPanelState).Name);
            await cancelHandler.Cancel(_controlPanelState, cancellationToken);
            LogCancelHandlerInvoked(_logger, typeof(TControlPanelState).Name);

            IsRunning = false;

            return true;
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here.
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully
        }

        return false;
    }

    private async Task<CancellationToken> GetCancellationTokenForHandlerMethodInvocation(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_handlerMethodInvocationCancellationTokenSource is not null)
            {
                await _handlerMethodInvocationCancellationTokenSource.CancelAsync();

                _handlerMethodInvocationCancellationTokenSource.Dispose();
            }

            _handlerMethodInvocationCancellationTokenSource = new CancellationTokenSource();

            return _handlerMethodInvocationCancellationTokenSource.Token;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [LoggerMessage(LogLevel.Warning, "Unexpected error while requesting cancellation of possible ongoing handler execution")]
    internal static partial void LogUnexpectedErrorWhileRequestingCancellation(ILogger<ControlPanelEdit<TControlPanelState>> logger,
        Exception exception);

    [LoggerMessage(LogLevel.Debug, "Invoking reset handler for state '{StateType}'")]
    internal static partial void LogResetHandlerInvoking(ILogger<ControlPanelEdit<TControlPanelState>> logger, string stateType);

    [LoggerMessage(LogLevel.Debug, "Reset handler invoked for state '{StateType}'")]
    internal static partial void LogResetHandlerInvoked(ILogger<ControlPanelEdit<TControlPanelState>> logger, string stateType);

    [LoggerMessage(LogLevel.Debug, "Invoking save handler for state '{StateType}'")]
    internal static partial void LogSaveHandlerInvoking(ILogger<ControlPanelEdit<TControlPanelState>> logger, string stateType);

    [LoggerMessage(LogLevel.Debug, "Save handler invoked for state '{StateType}'")]
    internal static partial void LogSaveHandlerInvoked(ILogger<ControlPanelEdit<TControlPanelState>> logger, string stateType);

    [LoggerMessage(LogLevel.Debug, "Invoking cancel handler for state '{StateType}'")]
    internal static partial void LogCancelHandlerInvoking(ILogger<ControlPanelEdit<TControlPanelState>> logger, string stateType);

    [LoggerMessage(LogLevel.Debug, "Cancel handler invoked for state '{StateType}'")]
    internal static partial void LogCancelHandlerInvoked(ILogger<ControlPanelEdit<TControlPanelState>> logger, string stateType);
}
