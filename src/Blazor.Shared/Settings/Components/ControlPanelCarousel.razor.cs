using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Factories;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;

namespace Blazor.Shared.Settings.Components;

public sealed partial class ControlPanelCarousel : ComponentBase, IAsyncDisposable
{
    private sealed class Item
    {
        public required IControlPanelRegistryItem ControlPanelRegistryItem { get; init; }

        // One visit: switching between the tabs of a parent and its children (see the control panel parent/child
        // mechanism) remounts ControlPanelContainer but keeps each control panel's edit and skips its reset,
        // so in-progress edits survive. Disposed when this Item is replaced or removed; its resets are cleared
        // when navigating back to it (see NavigateBackRequested).
        public required ControlPanelEditScope EditScope { get; init; }
    }

    /// <summary>
    /// A slide between the two top panels of the stack, kept until the browser reports the slide finished.
    /// </summary>
    private sealed class Transition
    {
        /// <summary>
        /// The panel the slide moves away from; the same <see cref="Item"/> instance stays in the items, so the carousel keeps
        /// it mounted until the slide ends.
        /// </summary>
        public required Item From { get; init; }

        /// <summary>
        /// <see cref="From"/> stays on top of the stack until the slide ends, so its pages remain active while it slides out.
        /// </summary>
        public required bool PopWhenEnded { get; init; }

        public required IControlPanelRegistryItem To { get; init; }
    }

    private readonly List<Item> _items = [];

    // Replaced and removed items, whose edit scopes are disposed outside the semaphore.
    private readonly List<Item> _removedItems = [];

    private int _activeIndex = -1;
    private Transition? _transition;
    private bool _shouldRender;

    private bool _disposed;

    private readonly SemaphoreSlim _settingsModuleStateSemaphore = new(1);

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    [Inject] private IActiveControlPanelChildProvider ActiveControlPanelChildProvider { get; set; } = default!;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject] private INavigateBackRequest NavigateBackRequest { get; set; } = default!;

    [Inject] private SettingsModuleState SettingsModuleState { get; set; } = default!;

    [Inject] private IControlPanelEditRegistry ControlPanelEditRegistry { get; set; } = default!;

    [Inject] private ControlPanelEditFactory ControlPanelEditFactory { get; set; } = default!;

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

        try
        {
            // The commit of a slide waits for its end, which no longer comes. It runs after the cleanup above because it
            // notifies subscribers, and one that throws must not skip it.
            EndTransition(_transition);
        }
        finally
        {
            _removedItems.AddRange(_items);
            _items.Clear();

            var removedItems = _removedItems.ToListAndClear();

            await DisposeEditScopes(removedItems);
        }
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

    private async Task SlideEnded(Item from)
    {
        if (_disposed)
            return;

        try
        {
            await _settingsModuleStateSemaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                if (_transition?.From == from)
                    EndTransition(_transition);
            }
            finally
            {
                var removedItems = _removedItems.ToListAndClear();

                _settingsModuleStateSemaphore.Release();

                await DisposeEditScopes(removedItems);
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
                var removedItems = _removedItems.ToListAndClear();

                _settingsModuleStateSemaphore.Release();

                await DisposeEditScopes(removedItems);
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

                // The control panel navigated back to was unmounted meanwhile and has to reload on remount, e.g. to
                // list a tag that was just added in the control panel navigated away from.
                _items[^2].EditScope.ClearResets();

                _transition = new Transition { From = _items[^1], PopWhenEnded = true, To = requestedControlPanels[^2] };

                UpdateItems();
            }
            finally
            {
                var removedItems = _removedItems.ToListAndClear();

                _settingsModuleStateSemaphore.Release();

                await DisposeEditScopes(removedItems);
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
                    SettingsModuleState.SetActiveChild(transition.From.ControlPanelRegistryItem, null);

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

    private IControlPanelRegistryItem ResolveActiveLeaf(IControlPanelRegistryItem item)
    {
        var current = item;

        while (ActiveControlPanelChildProvider.GetActiveChild(current) is { } activeChild)
            current = activeChild;

        return current;
    }

    private void UpdateItems()
    {
        var requestedControlPanelRegistryItems = SettingsModuleState.RequestedControlPanelRegistryItems;

        for (var i = 0; i < requestedControlPanelRegistryItems.Count; i++)
        {
            var controlPanelRegistryItem = requestedControlPanelRegistryItems[i];

            if (i >= _items.Count)
            {
                _items.Add(CreateItem(controlPanelRegistryItem));
            }
            else if (_items[i].ControlPanelRegistryItem != controlPanelRegistryItem)
            {
                _removedItems.Add(_items[i]);

                _items[i] = CreateItem(controlPanelRegistryItem);
            }
        }

        if (requestedControlPanelRegistryItems.Count < _items.Count)
        {
            var removeCount = _items.Count - requestedControlPanelRegistryItems.Count;

            _removedItems.AddRange(_items.GetRange(requestedControlPanelRegistryItems.Count, removeCount));
            _items.RemoveRange(requestedControlPanelRegistryItems.Count, removeCount);
        }

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

    private Item CreateItem(IControlPanelRegistryItem controlPanelRegistryItem)
        => new()
        {
            ControlPanelRegistryItem = controlPanelRegistryItem,
            EditScope = new ControlPanelEditScope(ControlPanelEditRegistry, ControlPanelEditFactory)
        };

    private static async Task DisposeEditScopes(IEnumerable<Item> items)
    {
        foreach (var item in items)
            await item.EditScope.DisposeAsync();
    }

    private async void SettingsModuleStateChanged(PropertiesChangedEventArgs args)
    {
        if (_disposed)
            return;

        // SetActiveControlPanel batches the reset of the left parent's active child with the switch, so a change of the
        // active control panel has to be checked first; otherwise leaving a parent from a child tab never updates the items.
        if (!args.PropertyNames.Contains(nameof(SettingsModuleState.ActiveControlPanelRegistryItem)) &&
            !args.PropertyNames.Contains(nameof(SettingsModuleState.RequestedControlPanelRegistryItems)))
        {
            if (args.PropertyNames.Contains(nameof(SettingsModuleState.ActiveChildMap)))
            {
                _shouldRender = true;

                await InvokeAsync(StateHasChanged);
            }

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
                var removedItems = _removedItems.ToListAndClear();

                _settingsModuleStateSemaphore.Release();

                await DisposeEditScopes(removedItems);
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
