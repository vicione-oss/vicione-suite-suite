using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;
using Sdk.Client.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed class ActiveControlPanelPageProvider : IActiveControlPanelPageProvider, IDisposable
{
    private readonly IControlPanelPageRegistry _controlPanelPageRegistry;
    private readonly SettingsModuleState _settingsModuleState;
    private readonly Dictionary<IControlPanelRegistryItem, IControlPanelPage> _activeControlPanelPageMap = [];
    private readonly HashSet<IControlPanelRegistryItem> _possibleControlPanelStateChangedSenders = [];

    public ActiveControlPanelPageProvider(IControlPanelPageRegistry controlPanelPageRegistry,
        SettingsModuleState settingsModuleState)
    {
        _controlPanelPageRegistry = controlPanelPageRegistry;
        _controlPanelPageRegistry.Changed += ControlPanelPageRegistryChanged;

        _settingsModuleState = settingsModuleState;
        _settingsModuleState.Changed += SettingsModuleStateChanged;

        IEnumerable<IControlPanelRegistryItem> observableControlPanelRegistryItems = _settingsModuleState.RequestedControlPanelRegistryItems;
        if (_settingsModuleState.ActiveControlPanelRegistryItem is not null)
            observableControlPanelRegistryItems = observableControlPanelRegistryItems.Append(_settingsModuleState.ActiveControlPanelRegistryItem);

        RecordActiveControlPanelPageAndObserve(observableControlPanelRegistryItems);
    }

    private void RecordActiveControlPanelPageAndObserve(IEnumerable<IControlPanelRegistryItem> controlPanelRegistryItems)
    {
        var staleControlPanelRegistryItems = _activeControlPanelPageMap.Keys.Except(controlPanelRegistryItems);

        foreach (var staleControlPanelRegistryItem in staleControlPanelRegistryItems)
        {
            _activeControlPanelPageMap.Remove(staleControlPanelRegistryItem);

            StopListenForControlPanelStateChanged(staleControlPanelRegistryItem);
        }

        foreach (var controlPanelRegistryItem in controlPanelRegistryItems)
        {
            UpdateActiveControlPanelPage(controlPanelRegistryItem);
            StartListenForControlPanelStateChanged(controlPanelRegistryItem);
        }
    }

    public void Dispose()
    {
        foreach (var controlPanelRegistryItem in _possibleControlPanelStateChangedSenders)
            StopListenForControlPanelStateChanged(controlPanelRegistryItem);

        _possibleControlPanelStateChangedSenders.Clear();
        _activeControlPanelPageMap.Clear();

        _settingsModuleState.Changed -= SettingsModuleStateChanged;
        _controlPanelPageRegistry.Changed -= ControlPanelPageRegistryChanged;
    }

    public IControlPanelPage? GetActiveControlPanelPage(IControlPanelRegistryItem controlPanelRegistryItem)
    {
        if (_activeControlPanelPageMap.TryGetValue(controlPanelRegistryItem, out var controlPanelPage))
            return controlPanelPage;

        return null;
    }

    private void ControlPanelPageRegistryChanged(RegistryChangedEventArgs<IControlPanelPageRegistryItem> args)
    {
        var mappedControlPanelRegistryItems = _activeControlPanelPageMap.Keys.ToList();

        foreach (var itemRemoved in args.ItemsRemoved)
        {
            if (_activeControlPanelPageMap.TryGetValue(itemRemoved.ControlPanelRegistryItem, out var controlPanelPage))
            {
                if (controlPanelPage == itemRemoved.ControlPanelPage)
                    _activeControlPanelPageMap.Remove(itemRemoved.ControlPanelRegistryItem);
            }
        }

        foreach (var g in args.ItemsAdded.ToLookup(i => i.ControlPanelRegistryItem))
            UpdateActiveControlPanelPage(g.Key);

        var oldControlPanelRegistryItems = mappedControlPanelRegistryItems.Except(_activeControlPanelPageMap.Keys);
        foreach (var oldControlPanelRegistryItem in oldControlPanelRegistryItems)
            StopListenForControlPanelStateChanged(oldControlPanelRegistryItem);

        var newControlPanelRegistryItems = _activeControlPanelPageMap.Keys.Except(mappedControlPanelRegistryItems);
        foreach (var newControlPanelRegistryItem in newControlPanelRegistryItems)
            StartListenForControlPanelStateChanged(newControlPanelRegistryItem);
    }

    private void SettingsModuleStateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(_settingsModuleState.ActiveControlPanelRegistryItem)) ||
            args.PropertyNames.Contains(nameof(_settingsModuleState.RequestedControlPanelRegistryItems)))
        {
            var observableControlPanelRegistryItems = _settingsModuleState.RequestedControlPanelRegistryItems.ToHashSet();
            if (_settingsModuleState.ActiveControlPanelRegistryItem is not null)
                observableControlPanelRegistryItems.Add(_settingsModuleState.ActiveControlPanelRegistryItem);

            RecordActiveControlPanelPageAndObserve(observableControlPanelRegistryItems);
        }
    }

    private void UpdateActiveControlPanelPage(IControlPanelRegistryItem controlPanelRegistryItem)
    {
        IEnumerable<IControlPanelPage> controlPanelPages = _controlPanelPageRegistry
            .Where(i => i.ControlPanelRegistryItem == controlPanelRegistryItem)
            .Select(i => i.ControlPanelPage)
            .ToList();

        var controlPanelPageCount = controlPanelPages.Count();

        var activeControlPanelPageIndex = controlPanelRegistryItem.State.ActivePageIndex ?? 0;
        if (activeControlPanelPageIndex >= controlPanelPageCount)
            activeControlPanelPageIndex = controlPanelPageCount - 1;

        if (activeControlPanelPageIndex > 0)
            controlPanelPages = controlPanelPages.Skip(activeControlPanelPageIndex);

        var activeControlPanelPage = controlPanelPages.Take(1).FirstOrDefault();
        if (activeControlPanelPage is not null)
            _activeControlPanelPageMap[controlPanelRegistryItem] = activeControlPanelPage;
        else
            _activeControlPanelPageMap.Remove(controlPanelRegistryItem);
    }

    private void StartListenForControlPanelStateChanged(IControlPanelRegistryItem controlPanelRegistryItem)
    {
        if (_possibleControlPanelStateChangedSenders.Add(controlPanelRegistryItem))
            controlPanelRegistryItem.State.Changed += ControlPanelStateChanged;
    }

    private void StopListenForControlPanelStateChanged(IControlPanelRegistryItem controlPanelRegistryItem)
    {
        if (_possibleControlPanelStateChangedSenders.Remove(controlPanelRegistryItem))
            controlPanelRegistryItem.State.Changed -= ControlPanelStateChanged;
    }

    private void ControlPanelStateChanged(ControlPanelStateChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(args.Sender.ActivePageIndex)))
        {
            var controlPanelRegistryItem = _possibleControlPanelStateChangedSenders.FirstOrDefault(i => i.State == args.Sender);
            if (controlPanelRegistryItem is not null)
                UpdateActiveControlPanelPage(controlPanelRegistryItem);
        }
    }
}
