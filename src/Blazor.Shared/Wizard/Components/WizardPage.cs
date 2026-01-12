using Blazor.Shared.Models;
using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Blazor.Shared.Wizard.Components;

public partial class WizardPage<TState> : ComponentBase, IWizardPage, IAsyncDisposable
    where TState : IWizardPageState
{
    private bool _initialized;
    private bool _disposedAsync;
    private IWizardPageSaveHandler<TState>? _saveHandler;
    private IWizardPageResetHandler<TState>? _resetHandler;
    private CancellationTokenSource? _handlerMethodInvocationCancellationTokenSource;
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly CancellationTokenSource _semaphoreCancellationTokenSource = new();

    [Parameter, EditorRequired] public TState State { get; set; }
    [Parameter, EditorRequired] public IWizardPageRegistryItem RegistryItem { get; set; }
    [Parameter] public EventCallback<IWizardOperation> OperationStarted { get; set; }

    [CascadingParameter] protected IWizard Wizard { get; private set; } = default!;
    [CascadingParameter] private IWizardPageEditService EditService { get; set; } = default!;
    [CascadingParameter] private IWizardBodyContentRenderCycle WizardBodyContentRenderCycle { get; set; } = default!;

    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private ILogger<WizardPage<TState>> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        if (_disposedAsync)
            return;

        _resetHandler ??= ServiceProvider.GetService<IWizardPageResetHandler<TState>>();

        if (_resetHandler is not null)
        {
            try
            {
                var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

                await _resetHandler.Reset(State, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Nothing to do here, return gracefully

                return;
            }
            catch (ObjectDisposedException)
            {
                // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully

                return;
            }
        }

        EditService.OnSave += EditServiceOnSave;
        EditService.OnCancel += EditServiceOnCancel;

        State.Changed += StateChanged;

        _initialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        await DisposeAsyncCore().ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        // For now, we do not call CancelEdit because it would require an await on the async behavior
        // which could result in memory leaks when the await that never returns

        State.Changed -= StateChanged;

        EditService.OnSave -= EditServiceOnSave;
        EditService.OnCancel -= EditServiceOnCancel;

        _semaphoreCancellationTokenSource.Cancel();
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
                    UnexpectedErrorWhileRequestingCancellation(Logger, e);
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

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender && _initialized)
            WizardBodyContentRenderCycle.SetFinished(RegistryItem);
    }

    private async Task<ISaveResult> EditServiceOnSave()
    {
        if (_disposedAsync)
            return new SaveSuccessResult();

        _saveHandler ??= ServiceProvider.GetService<IWizardPageSaveHandler<TState>>();

        if (_saveHandler is null)
            return new SaveSuccessResult("No save handler found");

        try
        {
            var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

            var saveResult = await _saveHandler.Save(State, cancellationToken);

            return saveResult;
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully

            return new SaveSuccessResult();
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully

            return new SaveSuccessResult();
        }
    }

    private async Task EditServiceOnCancel()
    {
        if (_disposedAsync)
            return;

        if (_resetHandler is null)
            return;

        try
        {
            var cancellationToken = await GetCancellationTokenForHandlerMethodInvocation(_semaphoreCancellationTokenSource.Token);

            await _resetHandler.Reset(State, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully
        }
    }

    /// <summary>
    /// Invoked when an edit takes place
    /// </summary>
    protected virtual Task OnEdit()
        => Task.CompletedTask;

    /// <summary>
    /// Call this to indicate that edit has begun.
    /// </summary>
    protected async Task BeginEdit()
    {
        await OnEdit();

        await EditService.BeginEdit();
    }

    /// <summary>
    /// Call this to request cancellation of an already running edit.
    /// </summary>
    protected Task CancelEdit()
        => EditService.CancelEdit();

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
    internal static partial void UnexpectedErrorWhileRequestingCancellation(ILogger<WizardPage<TState>> logger, Exception exception);

    private async void StateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(State.CurrentOperation)))
        {
            if (State.CurrentOperation is not null && OperationStarted.HasDelegate)
                await OperationStarted.InvokeAsync(State.CurrentOperation);
        }

        await InvokeAsync(StateHasChanged);
    }
}
