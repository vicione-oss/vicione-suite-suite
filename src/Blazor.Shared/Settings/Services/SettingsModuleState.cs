using System.Runtime.CompilerServices;
using Blazor.Shared.Interfaces;
using Blazor.Shared.Models;
using Blazor.Shared.Settings.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed class SettingsModuleState : IHasChangeableProperties, IHasUpdateLock
{
    private readonly Lock _concurrentLock = new();
    private readonly HashSet<string> _changedProperties = [];
    private int _updateLock;
    private SettingsCategory? _expandedSettingsCategory;
    private IControlPanelRegistryItem? _activeControlPanelRegistryItem;

    // The following field does not use Stack<> as there is no IReadOnlyStack<> and enumeration
    // from on the outside would overcomplicate things because it is done from the top-most element,
    // meaning we woud have negative logic pretty much anywhere
    private readonly List<IControlPanelRegistryItem> _requestedControlPanelRegistryItems = [];
    private readonly Dictionary<SettingsEntriesKey, IControlPanelRegistryItem> _lastActiveControlPanelRegistryItemMap = [];

    private bool _showNavigateBackButton;
    private bool _isLoadingOverlayVisible;

    /// <summary>
    /// Expanded settings category
    /// </summary>
    public SettingsCategory? ExpandedSettingsCategory
    {
        get => _expandedSettingsCategory;
        set
        {
            if (value != _expandedSettingsCategory)
            {
                _expandedSettingsCategory = value;

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Last active control panel per category
    /// </summary>
    public IReadOnlyDictionary<SettingsEntriesKey, IControlPanelRegistryItem> LastActiveControlPanelRegistryItemMap => _lastActiveControlPanelRegistryItemMap;

    /// <summary>
    /// Registry item of the control panel that is currently active
    /// </summary>
    public IControlPanelRegistryItem? ActiveControlPanelRegistryItem
    {
        get => _activeControlPanelRegistryItem;
        set
        {
            if (value != _activeControlPanelRegistryItem)
            {
                _activeControlPanelRegistryItem = value;

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Registry items of control panels in order they were requested either
    /// via accordion navigation or via <see cref="IControlPanelRequest"/>.
    /// </summary>
    /// <remarks>
    /// Accordion navigation reduces the list to contain only the registry item
    /// of the control panel requested.
    /// </remarks>
    public IReadOnlyList<IControlPanelRegistryItem> RequestedControlPanelRegistryItems => _requestedControlPanelRegistryItems;

    /// <inheritdoc/>
    public int UpdateLock => _updateLock;

    /// <summary>
    /// <see langword="true"/> when a navigate back button should be displayed, otherwise <see langword="false"/>
    /// </summary>
    public bool ShowNavigateBackButton
    {
        get => _showNavigateBackButton;
        set
        {
            if (value != _showNavigateBackButton)
            {
                _showNavigateBackButton = value;

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// <see langword="true"/> when a loading overlay should be displayed, otherwise <see langword="false"/>
    /// </summary>
    public bool IsLoadingOverlayVisible
    {
        get => _isLoadingOverlayVisible;
        set
        {
            if (value != _isLoadingOverlayVisible)
            {
                _isLoadingOverlayVisible = value;

                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public event Action<PropertiesChangedEventArgs>? Changed;

    public void AddOrSetLastActiveControlPanelRegistryItem(SettingsEntriesKey settingsEntryKey,
        IControlPanelRegistryItem controlPanelRegistryItem)
    {
        if (_lastActiveControlPanelRegistryItemMap.TryGetValue(settingsEntryKey, out var i))
        {
            if (i != controlPanelRegistryItem)
            {
                _lastActiveControlPanelRegistryItemMap[settingsEntryKey] = controlPanelRegistryItem;

                OnPropertyChanged(propertyName: nameof(LastActiveControlPanelRegistryItemMap));
            }
        }
        else
        {
            _lastActiveControlPanelRegistryItemMap[settingsEntryKey] = controlPanelRegistryItem;

            OnPropertyChanged(propertyName: nameof(LastActiveControlPanelRegistryItemMap));
        }
    }

    public void RemoveLastActiveControlPanelRegistryItem(SettingsEntriesKey settingsEntryKey)
    {
        if (_lastActiveControlPanelRegistryItemMap.Remove(settingsEntryKey))
            OnPropertyChanged(propertyName: nameof(LastActiveControlPanelRegistryItemMap));
    }

    public void ClearLastActiveControlPanelRegistryItemMap()
    {
        if (_lastActiveControlPanelRegistryItemMap.Count > 0)
        {
            _lastActiveControlPanelRegistryItemMap.Clear();

            OnPropertyChanged(propertyName: nameof(LastActiveControlPanelRegistryItemMap));
        }
    }

    public bool TryPushRequestedControlPanelRegistryItem(IControlPanelRegistryItem controlPanelRegistryItem)
    {
        if (_requestedControlPanelRegistryItems.LastOrDefault() == controlPanelRegistryItem)
            return false;

        _requestedControlPanelRegistryItems.Add(controlPanelRegistryItem);

        OnPropertyChanged(propertyName: nameof(RequestedControlPanelRegistryItems));

        return true;
    }

    public void PopRequestedControlPanelRegistryItem()
    {
        var lastItem = _requestedControlPanelRegistryItems.LastOrDefault();
        if (lastItem is not null)
        {
            _requestedControlPanelRegistryItems.Remove(lastItem);

            OnPropertyChanged(propertyName: nameof(RequestedControlPanelRegistryItems));
        }
    }

    public void ClearRequestedControlPanelRegistryItems()
    {
        if (_requestedControlPanelRegistryItems.Count > 0)
        {
            _requestedControlPanelRegistryItems.Clear();

            OnPropertyChanged(propertyName: nameof(RequestedControlPanelRegistryItems));
        }
    }

    /// <inheritdoc/>
    public void BeginUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock++;
        }
    }

    /// <inheritdoc/>
    public void EndUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock--;

            if (_updateLock > 0)
                return;

            _updateLock = 0;

            if (_changedProperties.Count == 0)
                return;

            Changed?.Invoke(new PropertiesChangedEventArgs(this, _changedProperties));

            _changedProperties.Clear();
        }
    }

    /// <summary>
    /// Raises a <see cref="Changed">Changed</see> event when no <see cref="BeginUpdate">update cycle</see> is running.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is null)
            return;

        if (_updateLock == 0)
            Changed?.Invoke(new PropertiesChangedEventArgs(this, new HashSet<string> { propertyName }));
        else
            _changedProperties.Add(propertyName);
    }
}
