using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.Models;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Wizards.Models;

internal sealed partial class WizardPageEdit<TWizardPageState>(TWizardPageState wizardPageState, IServiceProvider serviceProvider)
    : IWizardPageEdit
        where TWizardPageState : class, IWizardPageState
{
    private readonly ILogger<WizardPageEdit<TWizardPageState>> _logger =
        serviceProvider.GetRequiredService<ILogger<WizardPageEdit<TWizardPageState>>>();

    private readonly Lazy<IWizardPageResetHandler<TWizardPageState>?> _resetHandler =
        new(() => serviceProvider.GetService<IWizardPageResetHandler<TWizardPageState>>());

    private readonly Lazy<IWizardPageSaveHandler<TWizardPageState>?> _saveHandler =
        new(() => serviceProvider.GetService<IWizardPageSaveHandler<TWizardPageState>>());

    private readonly Lazy<IWizardPageCancelHandler<TWizardPageState>?> _cancelHandler =
        new(() => serviceProvider.GetService<IWizardPageCancelHandler<TWizardPageState>>());

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
                    UnexpectedErrorWhileRequestingCancellation(_logger, e);
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
            return true; // handle missing handler as if reset was successful as otherwise handler should be mandatory

        try
        {
            var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

            await resetHandler.Reset(wizardPageState, cancellationToken);

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
        // Deliberately no IsRunning check: a finish step has nothing editable, so its save would
        // never have begun an edit, yet it still has to run.
        var saveHandler = _saveHandler.Value;
        if (saveHandler == null)
            return new SaveSuccessResult(); // handle missing handler as if save was successful as otherwise handler should be mandatory

        try
        {
            var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

            var saveResult = await saveHandler.Save(wizardPageState, cancellationToken);

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

            await cancelHandler.Cancel(wizardPageState, cancellationToken);

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

    [LoggerMessage(1, LogLevel.Warning, "Unexpected error while requesting cancellation of possible ongoing handler execution")]
    internal static partial void UnexpectedErrorWhileRequestingCancellation(ILogger<WizardPageEdit<TWizardPageState>> logger,
        Exception exception);
}
