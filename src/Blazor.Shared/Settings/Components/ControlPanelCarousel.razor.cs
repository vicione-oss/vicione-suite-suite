using System.Collections.Concurrent;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;

namespace Blazor.Shared.Settings.Components;

public sealed partial class ControlPanelCarousel : ComponentBase, IDisposable
{
    private sealed class Item
    {
        public required IControlPanelRegistryItem ControlPanelRegistryItem { get; set; }
        public required bool Active { get; set; }
    }

    private enum AnimationKind
    {
        SlideLeft,
        SlideRight
    }

    private sealed class ItemAnimation
    {
        public required IControlPanelRegistryItem? Item { get; init; }
        public required AnimationKind Kind { get; init; }
        public int DurationMs { get; private set; } = 200;
    }

    private enum ItemTransitionDirection
    {
        Next,
        Previous
    }

    private sealed class ItemTransition
    {
        public required IControlPanelRegistryItem? From { get; init; }
        public required IControlPanelRegistryItem? To { get; init; }
        public required ItemTransitionDirection Direction { get; init; }
        public required ItemAnimation Animation { get; init; }
    }

    private readonly List<Item> _items = [];
    private readonly ConcurrentQueue<ItemTransition> _itemTransitions = [];
    private bool _shouldRender;

    private readonly SemaphoreSlim _settingsModuleStateSemaphore = new(1);

    [Inject] private IControlPanelPageRegistry ControlPanelPageRegistry { get; set; } = default!;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    [Inject] private INavigateBackRequest NavigateBackRequest { get; set; } = default!;

    [Inject] private SettingsModuleState SettingsModuleState { get; set; } = default!;

    protected override void OnInitialized()
    {
        UpdateItems(SettingsModuleState.RequestedControlPanelRegistryItems);

        ControlPanelRequest.ControlPanelRequested += ControlPanelRequested;
        NavigateBackRequest.NavigateBackRequested += NavigateBackRequested;
        SettingsModuleState.Changed += SettingsModuleStateChanged;
    }

    public void Dispose()
    {
        SettingsModuleState.Changed -= SettingsModuleStateChanged;
        NavigateBackRequest.NavigateBackRequested -= NavigateBackRequested;
        ControlPanelRequest.ControlPanelRequested -= ControlPanelRequested;

        _settingsModuleStateSemaphore.Dispose();
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

    protected override async void OnAfterRender(bool firstRender)
    {
        if (_itemTransitions.TryDequeue(out var itemTranstion))
        {
            await Task.Delay(itemTranstion.Animation.DurationMs); // give browser some time to animate

            await _settingsModuleStateSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                SettingsModuleState.BeginUpdate();
                try
                {
                    SettingsModuleState.ActiveControlPanelRegistryItem = itemTranstion.To;

                    if (itemTranstion.Direction == ItemTransitionDirection.Next)
                    {
                        SettingsModuleState.ShowNavigateBackButton = true;
                    }
                    else if (itemTranstion.Direction == ItemTransitionDirection.Previous)
                    {
                        ResetActiveControlPanelPage(itemTranstion.From);

                        SettingsModuleState.PopRequestedControlPanelRegistryItem();
                        SettingsModuleState.ShowNavigateBackButton = SettingsModuleState.RequestedControlPanelRegistryItems.Count > 1;
                    }
                }
                finally
                {
                    SettingsModuleState.EndUpdate();
                }
            }
            finally
            {
                _settingsModuleStateSemaphore.Release();
            }
        }
    }

    private static void ResetActiveControlPanelPage(IControlPanelRegistryItem? controlPanelRegistryItem)
    {
        if (controlPanelRegistryItem is not null)
            controlPanelRegistryItem.State.ActivePageIndex = null;
    }

    private async Task ControlPanelRequested(ControlPanelRequestedEventArgs args)
    {
        if (args.Cancel)
            return;

        await _settingsModuleStateSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!SettingsModuleState.TryPushRequestedControlPanelRegistryItem(args.RegistryItem))
                return;

            UpdateItems(SettingsModuleState.RequestedControlPanelRegistryItems);

            args.ConfigureState?.Invoke();

            var activeControlPanel = _items.FirstOrDefault(i => i.Active)?.ControlPanelRegistryItem;
            var requestedControlPanel = args.RegistryItem;

            var itemTransition = new ItemTransition
            {
                From = activeControlPanel,
                To = requestedControlPanel,
                Direction = ItemTransitionDirection.Next,
                Animation = new ItemAnimation { Item = activeControlPanel, Kind = AnimationKind.SlideLeft }
            };

            _itemTransitions.Enqueue(itemTransition);
        }
        finally
        {
            _settingsModuleStateSemaphore.Release();
        }

        _shouldRender = true;

        await InvokeAsync(StateHasChanged);
    }

    private async Task NavigateBackRequested(NavigateBackRequestedEventArgs args)
    {
        if (args.Cancel)
            return;

        await _settingsModuleStateSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var activeControlPanelRequestDescriptor = SettingsModuleState.RequestedControlPanelRegistryItems
                .Select((controlPanelRegistryItem, index) => new { ControlPanelRegistryItem = controlPanelRegistryItem, Index = index })
                .FirstOrDefault(i => i.ControlPanelRegistryItem == SettingsModuleState.ActiveControlPanelRegistryItem);

            if (activeControlPanelRequestDescriptor == null || activeControlPanelRequestDescriptor.Index < 1)
                return;

            var controlPanelRequestedBeforeActiveControlPanel = SettingsModuleState.RequestedControlPanelRegistryItems
                .Skip(activeControlPanelRequestDescriptor.Index - 1)
                .FirstOrDefault();

            if (controlPanelRequestedBeforeActiveControlPanel == null)
                return;

            var activeControlPanel = activeControlPanelRequestDescriptor.ControlPanelRegistryItem;
            var requestedControlPanel = controlPanelRequestedBeforeActiveControlPanel;

            var itemTransition = new ItemTransition
            {
                From = activeControlPanel,
                To = requestedControlPanel,
                Direction = ItemTransitionDirection.Previous,
                Animation = new ItemAnimation { Item = requestedControlPanel, Kind = AnimationKind.SlideRight }
            };

            _itemTransitions.Enqueue(itemTransition);
        }
        finally
        {
            _settingsModuleStateSemaphore.Release();
        }

        _shouldRender = true;

        await InvokeAsync(StateHasChanged);
    }

    private void UpdateItems(IReadOnlyList<IControlPanelRegistryItem> requestedControlPanelRegistryItems)
    {
        for (var i = 0; i < requestedControlPanelRegistryItems.Count; i++)
        {
            var controlPanelRegistryItem = requestedControlPanelRegistryItems[i];
            var isActive = controlPanelRegistryItem == SettingsModuleState.ActiveControlPanelRegistryItem;

            if (i < _items.Count)
            {
                var item = _items[i];
                if (item.ControlPanelRegistryItem != controlPanelRegistryItem)
                {
                    _items[i] = new() { ControlPanelRegistryItem = controlPanelRegistryItem, Active = isActive };
                }
                else
                {
                    item.Active = isActive;
                }
            }
            else
            {
                _items.Add(new() { ControlPanelRegistryItem = controlPanelRegistryItem, Active = isActive });
            }
        }

        if (requestedControlPanelRegistryItems.Count < _items.Count)
            _items.RemoveRange(requestedControlPanelRegistryItems.Count, _items.Count - requestedControlPanelRegistryItems.Count);
    }

    private async void SettingsModuleStateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(SettingsModuleState.ActiveControlPanelRegistryItem)))
        {
            await _settingsModuleStateSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                UpdateItems(SettingsModuleState.RequestedControlPanelRegistryItems);
            }
            finally
            {
                _settingsModuleStateSemaphore.Release();
            }

            _shouldRender = true;

            await InvokeAsync(StateHasChanged);
        }
    }
}
