using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Extensions;
using Sdk.Client.Models;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Components;

public sealed partial class ControlPanelCarousel : ComponentBase, IAsyncDisposable
{
    private sealed class Item
    {
        public required IControlPanelRegistryItem ControlPanelRegistryItem { get; init; }
    }

    /// <summary>
    /// A slide between the two top panels of the stack, kept until the browser reports the slide finished.
    /// </summary>
    private sealed class Transition
    {
        /// <summary>
        /// The panel the slide moves away from; the same <see cref="Item"/> instance is kept in the items, so Blazor keeps it
        /// mounted until the slide ends.
        /// </summary>
        public required Item From { get; init; }

        /// <summary>
        /// <see cref="From"/> stays on top of the stack until the slide ends, so its pages remain active while it slides out.
        /// </summary>
        public required bool PopWhenEnded { get; init; }

        public required IControlPanelRegistryItem To { get; init; }

        public bool EndAwaited { get; set; }
    }

    private readonly List<Item> _items = [];
    private int _activeIndex = -1;
    private Transition? _transition;
    private bool _shouldRender;

    private ElementReference _track;
    private IJSObjectReference? _jsModule;

    private bool _disposed;

    private readonly SemaphoreSlim _settingsModuleStateSemaphore = new(1);

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    [Inject] private IControlPanelPageRegistry ControlPanelPageRegistry { get; set; } = default!;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject] private INavigateBackRequest NavigateBackRequest { get; set; } = default!;

    [Inject] private SettingsModuleState SettingsModuleState { get; set; } = default!;

    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    [Inject] private ILogger<ControlPanelCarousel> Logger { get; set; } = default!;

    protected override void OnInitialized()
    {
        UpdateItems();

        ControlPanelRequest.ControlPanelRequested += ControlPanelRequested;
        NavigateBackRequest.NavigateBackRequested += NavigateBackRequested;
        SettingsModuleState.Changed += SettingsModuleStateChanged;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false))
            return;

        SettingsModuleState.Changed -= SettingsModuleStateChanged;
        NavigateBackRequest.NavigateBackRequested -= NavigateBackRequested;
        ControlPanelRequest.ControlPanelRequested -= ControlPanelRequested;

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        _settingsModuleStateSemaphore.Dispose();

        await _jsModule.TryDisposeAsync(Logger);

        // The commit of a slide waits for its end, which no longer comes. It runs last because it notifies subscribers, and
        // one that throws must not skip the cleanup above.
        EndTransition(_transition);
    }

    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;

            return true;
        }

        return false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _transition is not { EndAwaited: false } transition)
            return;

        transition.EndAwaited = true;

        try
        {
            try
            {
                _jsModule ??= await JsInterop.IncludeModuleScript<SharedClientModule>("control-panel-carousel.js", CancellationToken);

                // The import ignores cancellation, so a carousel disposed meanwhile has not released the module yet.
                if (_disposed)
                {
                    await _jsModule.TryDisposeAsync(Logger);

                    return;
                }

                if (_jsModule is not null)
                    await _jsModule.InvokeVoidAsync("waitForTransitionEnd", CancellationToken, _track);
            }
            catch (JSException ex)
            {
                // A failed wait ends the slide anyway, so the panel sliding out does not stay mounted off-screen.
                Logger.LogWarning(ex, "Waiting for the control panel slide to end failed.");
            }
            catch (OperationCanceledException ex) when (!_disposed)
            {
                // The JS interop call timed out; the slide ends anyway, so the panel sliding out does not stay mounted off-screen.
                Logger.LogWarning(ex, "Waiting for the control panel slide to end timed out.");
            }

            await _settingsModuleStateSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                EndTransition(transition);
            }
            finally
            {
                _settingsModuleStateSemaphore.Release();
            }

            _shouldRender = true;

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // The carousel was disposed (e.g. settings closed) mid-slide; nothing left to clean up.
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource behind CancellationToken already disposed, nothing we can do, return gracefully.
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone mid-slide; nothing left to clean up.
        }
    }

    private async Task ControlPanelRequested(ControlPanelRequestedEventArgs args)
    {
        if (_disposed || args.Cancel)
            return;

        try
        {
            await _settingsModuleStateSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                EndTransition(_transition);

                UpdateItems();

                // A slide runs between the two top panels of the stack, so it only starts from an active panel on top.
                var from = _activeIndex >= 0 && _activeIndex == _items.Count - 1 ? _items[^1] : null;

                SettingsModuleState.BeginUpdate();
                try
                {
                    if (!SettingsModuleState.TryPushRequestedControlPanelRegistryItem(args.RegistryItem))
                        return;

                    args.ConfigureState?.Invoke();

                    if (from is not null)
                    {
                        _transition = new Transition { From = from, PopWhenEnded = false, To = args.RegistryItem };
                    }
                    else
                    {
                        SettingsModuleState.ActiveControlPanelRegistryItem = args.RegistryItem;
                        SettingsModuleState.ShowNavigateBackButton = true;
                    }
                }
                finally
                {
                    SettingsModuleState.EndUpdate();
                }

                UpdateItems();
            }
            finally
            {
                _settingsModuleStateSemaphore.Release();
            }

            _shouldRender = true;

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // Disposed while awaiting; nothing to do.
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource behind CancellationToken already disposed, nothing we can do, return gracefully.
        }
    }

    private async Task NavigateBackRequested(NavigateBackRequestedEventArgs args)
    {
        if (_disposed || args.Cancel)
            return;

        try
        {
            await _settingsModuleStateSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                // Commits a running slide first, so a quick second navigate-back moves one level further.
                EndTransition(_transition);

                var requestedControlPanels = SettingsModuleState.RequestedControlPanelRegistryItems;
                if (requestedControlPanels.Count < 2)
                    return;

                UpdateItems();

                _transition = new Transition { From = _items[^1], PopWhenEnded = true, To = requestedControlPanels[^2] };

                UpdateItems();
            }
            finally
            {
                _settingsModuleStateSemaphore.Release();
            }

            _shouldRender = true;

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // Disposed while awaiting; nothing to do.
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource behind CancellationToken already disposed, nothing we can do, return gracefully.
        }
    }

    private void EndTransition(Transition? transition)
    {
        if (transition is null || transition != _transition)
            return;

        _transition = null;

        // Settings navigation during the slide has replaced the stack already, so there is nothing left to commit.
        if (IsCurrent(transition))
        {
            // Committed when the slide ends, so the header tabs, the accordion and the pages of the panel sliding out stay
            // with it while it is still visible.
            SettingsModuleState.BeginUpdate();
            try
            {
                SettingsModuleState.ActiveControlPanelRegistryItem = transition.To;

                if (transition.PopWhenEnded)
                {
                    transition.From.ControlPanelRegistryItem.State.ActivePageIndex = null;

                    SettingsModuleState.PopRequestedControlPanelRegistryItem();
                }

                SettingsModuleState.ShowNavigateBackButton = SettingsModuleState.RequestedControlPanelRegistryItems.Count > 1;
            }
            finally
            {
                SettingsModuleState.EndUpdate();
            }
        }

        UpdateItems();
    }

    private bool IsCurrent(Transition transition)
    {
        var requestedControlPanelRegistryItems = SettingsModuleState.RequestedControlPanelRegistryItems;

        var (top, belowTop) = transition.PopWhenEnded
            ? (transition.From.ControlPanelRegistryItem, transition.To)
            : (transition.To, transition.From.ControlPanelRegistryItem);

        return requestedControlPanelRegistryItems.Count >= 2 &&
               requestedControlPanelRegistryItems[^1] == top &&
               requestedControlPanelRegistryItems[^2] == belowTop;
    }

    private void UpdateItems()
    {
        var requestedControlPanelRegistryItems = SettingsModuleState.RequestedControlPanelRegistryItems;

        for (var i = 0; i < requestedControlPanelRegistryItems.Count; i++)
        {
            var controlPanelRegistryItem = requestedControlPanelRegistryItems[i];

            if (i >= _items.Count)
                _items.Add(new() { ControlPanelRegistryItem = controlPanelRegistryItem });
            else if (_items[i].ControlPanelRegistryItem != controlPanelRegistryItem)
                _items[i] = new() { ControlPanelRegistryItem = controlPanelRegistryItem };
        }

        if (requestedControlPanelRegistryItems.Count < _items.Count)
            _items.RemoveRange(requestedControlPanelRegistryItems.Count, _items.Count - requestedControlPanelRegistryItems.Count);

        if (_transition is not null)
        {
            // The track already shows the target, while the panel sliding out is still the active one.
            if (_transition.PopWhenEnded)
                _activeIndex = requestedControlPanelRegistryItems.Count - 2;
            else
                _activeIndex = requestedControlPanelRegistryItems.Count - 1;
        }
        else if (SettingsModuleState.ActiveControlPanelRegistryItem is { } activeControlPanelRegistryItem)
        {
            // The last occurrence, because the same panel can be on the stack twice and the active one is the latest.
            _activeIndex = requestedControlPanelRegistryItems.ToList().LastIndexOf(activeControlPanelRegistryItem);
        }
        else
        {
            _activeIndex = -1;
        }
    }

    private async void SettingsModuleStateChanged(PropertiesChangedEventArgs args)
    {
        if (_disposed)
            return;

        if (!args.PropertyNames.Contains(nameof(SettingsModuleState.ActiveControlPanelRegistryItem)) &&
            !args.PropertyNames.Contains(nameof(SettingsModuleState.RequestedControlPanelRegistryItems)))
        {
            return;
        }

        try
        {
            await _settingsModuleStateSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                // Navigation outside the carousel, e.g. via the accordion, replaces the panel without sliding.
                if (_transition is not null && !IsCurrent(_transition))
                    EndTransition(_transition);

                UpdateItems();
            }
            finally
            {
                _settingsModuleStateSemaphore.Release();
            }

            _shouldRender = true;

            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // Disposed while awaiting; nothing to do.
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource behind CancellationToken already disposed, nothing we can do, return gracefully.
        }
    }
}
