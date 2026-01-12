using System.Collections.Concurrent;
using System.Globalization;
using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.Models;
using Blazor.Shared.Popup.Factories;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using ViciOne.Ui.Blazor.Components.LoadingSpinner.Models;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsContainer : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1);

    private List<SettingsGroup> _settingsGroups = [];
    private readonly ConcurrentDictionary<SettingsGroup, Dictionary<string, SettingsCategory>> _settingsCategoryMaps = [];
    private Dictionary<SettingsEntriesKey, IEnumerable<SettingsEntry>> _settingsEntriesMap = [];
    private ControlPanelRequestedEventArgs? _requestedControlPanelRequestedEventArgs;
    private bool _requestedNavigateBack;
    private SettingsEntriesKey? _requestedExpandSettingsCategory;
    private SettingsEntry? _requestedSettingsEntry;
    private SettingsEntry? _activeSettingsEntry;

    private List<TimedMessage>? _loadingSpinnerTimedMessages;

    private bool _initialized;

    private ControlPanelStateFinishResult? LastSaveResult { get; set; }
    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    private List<TimedMessage> LoadingSpinnerTimedMessages => _loadingSpinnerTimedMessages ??= TimedMessagesFactory.CreateTimedMessages().ToList();

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback OnHasUnsavedChanges { get; set; }

    [Inject] private IControlPanelService ControlPanelService { get; set; } = default!;
    [Inject] private ILogger<SettingsContainer> Logger { get; set; } = default!;
    [Inject] private SettingsModuleService SettingsService { get; set; } = default!;
    [Inject] private SettingsModuleState State { get; set; } = default!;
    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] private INavigateBackRequest NavigateBackRequest { get; set; } = default!;
    [Inject] private IControlPanelRegistryItemCache ControlPanelRegistryItemCache { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private ILoadingIndicationPlacementBehavior LoadingIndicationPlacementBehavior { get; set; } = default!;

    public void Dispose()
    {
        State.Changed -= StateChanged;
        State.ClearLastActiveControlPanelRegistryItemMap();

        ControlPanelService.OnCancelEdit -= ChildStateOnBeginOrCancelEdit;
        ControlPanelService.OnBeginEdit -= ChildStateOnBeginOrCancelEdit;

        NavigateBackRequest.NavigateBackRequested -= NavigateBackRequested;
        ControlPanelRequest.ControlPanelRequested -= ControlPanelRequested;

        ControlPanelRegistryItemCache.Changed -= ControlPanelRegistryItemCacheChanged;

        LoadingIndicationPlacementBehavior.PlacementChanged -= LoadingIndicationPlacementChanged;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _updateSemaphore.Dispose();
    }

    private void CancelIntermediateRequests()
    {
        _requestedControlPanelRequestedEventArgs = null;
        _requestedNavigateBack = false;
        _requestedExpandSettingsCategory = null;
        _requestedSettingsEntry = null;
    }

    private async Task ExecuteIntermediateRequests()
    {
        if (_requestedControlPanelRequestedEventArgs is not null)
        {
            await ControlPanelRequest.Send(_requestedControlPanelRequestedEventArgs.RegistryItem,
                _requestedControlPanelRequestedEventArgs.ConfigureState);

            _requestedControlPanelRequestedEventArgs = null;
        }

        if (_requestedNavigateBack)
        {
            await NavigateBackRequest.Send();

            _requestedNavigateBack = false;
        }

        if (_requestedExpandSettingsCategory is not null)
        {
            AdoptToCategoryExpand(_requestedExpandSettingsCategory);

            _requestedExpandSettingsCategory = null;
        }

        if (_requestedSettingsEntry is not null)
            AdoptRequestedSettingsEntry();
    }

    internal void OnCancelUnsavedChangesPopup()
        => CancelIntermediateRequests();

    internal async Task OnRevertUnsavedChangesPopup()
    {
        try
        {
            await ControlPanelService.CancelEdit();

            await ExecuteIntermediateRequests();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unexpected error occurred while requesting cancel edit");

            LastSaveResult = new ControlPanelStateFinishResult(false,
                string.Format(CultureInfo.CurrentCulture, Localization.SettingsContainer.UnexpectedErrorWhileCancelEdit, e.Message));

            CancelIntermediateRequests();

            await InvokeAsync(StateHasChanged);
        }
    }

    internal async Task OnSaveUnsavedChangesPopup()
    {
        LastSaveResult = await ControlPanelService.FinishEdit();

        if (LastSaveResult.Success)
        {
            await ExecuteIntermediateRequests();

            await Update();
        }
        else
        {
            CancelIntermediateRequests();

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<IEnumerable<IControlPanelRegistryItem>> GetControlPanelRegistryItem(CancellationToken cancellationToken)
    {
        var user = await AuthenticationStateProvider.GetUser();

        var controlPanelRegistryItems = await ControlPanelRegistryItemCache.GetAll(user, cancellationToken);

        return controlPanelRegistryItems;
    }

    protected override async Task OnInitializedAsync()
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        var controlPanelRegistryItems = (await GetControlPanelRegistryItem(CancellationToken)).ToArray();

        UpdateAccordionData(controlPanelRegistryItems, preselectFirstSettingsEntryInFirstSettingsGroup: true);

        UpdateActiveControlPanel(controlPanelRegistryItems);
        UpdateActiveSettingsEntry();

        ControlPanelService.OnBeginEdit += ChildStateOnBeginOrCancelEdit;
        ControlPanelService.OnCancelEdit += ChildStateOnBeginOrCancelEdit;

        State.Changed += StateChanged;

        ControlPanelRequest.ControlPanelRequested += ControlPanelRequested;
        NavigateBackRequest.NavigateBackRequested += NavigateBackRequested;

        ControlPanelRegistryItemCache.Changed += ControlPanelRegistryItemCacheChanged;

        LoadingIndicationPlacementBehavior.PlacementChanged += LoadingIndicationPlacementChanged;

        _initialized = true;
    }

    private async void ControlPanelRegistryItemCacheChanged()
    {
        CancelIntermediateRequests();

        try
        {
            await Update();
        }
        catch (ObjectDisposedException)
        {
            // CurrentDomain: err=System.ObjectDisposedException: Cannot access a disposed object.
            // Object name: 'System.Threading.SemaphoreSlim'.
            // at System.Threading.SemaphoreSlim.Release(Int32 releaseCount)
            // at Blazor.Shared.Settings.Components.SettingsContainer.Update() in \src\Blazor.Shared\Settings\Components\SettingsContainer.razor.cs:line 288
            // at Blazor.Shared.Settings.Components.SettingsContainer.Update()
            // at Blazor.Shared.Settings.Components.SettingsContainer.ControlPanelRegistryItemCacheChanged() in \src\Blazor.Shared\Settings\Components\SettingsContainer.razor.cs:line 198
            Logger.LogWarning("Update control panel registry caused ObjectDisposedException");
        }
    }

    private bool UpdateActiveControlPanel(ICollection<IControlPanelRegistryItem> controlPanelRegistryItems)
    {
        var activeControlPanelRegistryItem = State.ActiveControlPanelRegistryItem;

        if (activeControlPanelRegistryItem is not null)
        {
            // check whether active ControlPanelRegistryItem is still the same
            if (!controlPanelRegistryItems.Contains(activeControlPanelRegistryItem))
                activeControlPanelRegistryItem = null;

            activeControlPanelRegistryItem ??= TryGetActiveControlPanelFromRequestedControlPanelChain(controlPanelRegistryItems);
        }

        if (activeControlPanelRegistryItem is null)
        {
            var firstSettingsGroup = _settingsGroups.FirstOrDefault();
            if (firstSettingsGroup is not null)
            {
                var firstSettingsCategory = _settingsCategoryMaps[firstSettingsGroup].Values.Sort().FirstOrDefault();
                if (firstSettingsCategory is not null)
                {
                    if (_settingsEntriesMap.TryGetValue(firstSettingsGroup, firstSettingsCategory, out var settingsEntries))
                    {
                        activeControlPanelRegistryItem = settingsEntries.FirstOrDefault()?.ControlPanelRegistryItem;
                    }
                }
            }
        }

        if (activeControlPanelRegistryItem != State.ActiveControlPanelRegistryItem)
        {
            SetActiveControlPanel(activeControlPanelRegistryItem);

            return true;
        }
        else
        {
            return false;
        }
    }

    private SettingsEntriesKey? GetSettingsEntriesKey(IControlPanelRegistryItem? controlPanelRegistryItem)
    {
        var settingsEntriesKey = _settingsEntriesMap
            .Where(p => p.Value.Any(settingsEntry => settingsEntry.ControlPanelRegistryItem == controlPanelRegistryItem))
            .Select(p => p.Key)
            .FirstOrDefault();

        return settingsEntriesKey;
    }

    private bool UpdateStateExpandedSettingsCategory(SettingsCategory? settingsCategory)
    {
        if (settingsCategory != State.ExpandedSettingsCategory)
        {
            State.ExpandedSettingsCategory = settingsCategory;

            return true;
        }
        else
        {
            return false;
        }
    }

    private async Task Update()
    {
        await _updateSemaphore.WaitAsync(CancellationToken);
        try
        {
            var controlPanelRegistryItems = (await GetControlPanelRegistryItem(CancellationToken)).ToArray();

            UpdateAccordionData(controlPanelRegistryItems);

            var stateChanged = UpdateActiveControlPanel(controlPanelRegistryItems);

            stateChanged |= UpdateActiveSettingsEntry();

            if (stateChanged)
                await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // nothing to do here, we just return gracefully
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    private bool UpdateActiveSettingsEntry()
    {
        var previousActiveSettingsEntry = _activeSettingsEntry;

        _activeSettingsEntry = _settingsEntriesMap.Values.SelectMany(settingsEntries => settingsEntries)
            .FirstOrDefault(s => s.ControlPanelRegistryItem == State.ActiveControlPanelRegistryItem);

        _activeSettingsEntry ??= TryGetActiveSettingsEntryFromRequestedControlPanelChain();

        return _activeSettingsEntry != previousActiveSettingsEntry;
    }

    private SettingsEntry? TryGetActiveSettingsEntryFromRequestedControlPanelChain()
    {
        var controlPanelRegistryCandidates = State.RequestedControlPanelRegistryItems.Reverse();

        foreach (var controlPanelRegistryCandidate in controlPanelRegistryCandidates)
        {
            var activeSettingsEntry = _settingsEntriesMap.Values.SelectMany(settingsEntries => settingsEntries)
                .FirstOrDefault(s => s.ControlPanelRegistryItem == controlPanelRegistryCandidate);

            if (activeSettingsEntry is not null)
                return activeSettingsEntry;
        }

        return null;
    }

    private IControlPanelRegistryItem? TryGetActiveControlPanelFromRequestedControlPanelChain(IEnumerable<IControlPanelRegistryItem> controlPanelRegistryItems)
    {
        var controlPanelRegistryCandidates = State.RequestedControlPanelRegistryItems;
        if (controlPanelRegistryCandidates.Count < 1)
            return null;

        if (!controlPanelRegistryCandidates.All(controlPanelRegistryItems.Contains))
        {
            // At least one of the candidates is not known anymore, so at least one candidate is not accessible anymore and we have to
            // handle this case somehow. Since no mechanism exists to safely navigate back to an accessible candidate, we handle this
            // case by returning null to ensure depending states will be reset.
            return null;
        }

        var lastRequestedControlPanelRegistryItem = controlPanelRegistryCandidates[controlPanelRegistryCandidates.Count - 1];

        return lastRequestedControlPanelRegistryItem;
    }

    private async void StateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(State.ActiveControlPanelRegistryItem)))
        {
            var settingsEntriesKey = GetSettingsEntriesKey(State.ActiveControlPanelRegistryItem);

            if (settingsEntriesKey is not null)
                UpdateLastActiveControlPanelRegistryItem(settingsEntriesKey, State.ActiveControlPanelRegistryItem);

            var invokeStateHasChanged = false;

            if (settingsEntriesKey is not null)
                invokeStateHasChanged |= UpdateStateExpandedSettingsCategory(settingsEntriesKey.SettingsCategory);

            invokeStateHasChanged |= UpdateActiveSettingsEntry();

            if (invokeStateHasChanged)
                await InvokeAsync(StateHasChanged);
        }
        else if (args.PropertyNames.Contains(nameof(State.IsLoadingOverlayVisible)))
        {
            if (State.IsLoadingOverlayVisible)
                await LoadingIndicationPlacementBehavior.Attach(CancellationToken);
            else
                await LoadingIndicationPlacementBehavior.Remove(CancellationToken);

            await InvokeAsync(StateHasChanged);
        }
    }

    private async void LoadingIndicationPlacementChanged()
        => await InvokeAsync(StateHasChanged);

    private void UpdateAccordionData(ICollection<IControlPanelRegistryItem> controlPanelRegistryItems,
        bool preselectFirstSettingsEntryInFirstSettingsGroup = false)
    {
        _settingsGroups = SettingsService.GetSettingsGroups(controlPanelRegistryItems);

        _settingsCategoryMaps.Clear();
        foreach (var settingsGroup in _settingsGroups)
        {
            var settingsCategoryMap = SettingsService.GetSettingsCategoryMap(controlPanelRegistryItems, settingsGroup.Position);

            _settingsCategoryMaps.TryAdd(settingsGroup, settingsCategoryMap);
        }

        _settingsEntriesMap = _settingsCategoryMaps
            .SelectMany(p => p.Value.Values
                .Select(settingsCategory => new SettingsEntriesKey { SettingsGroup = p.Key, SettingsCategory = settingsCategory }))
            .ToDictionary(
                settingsEntriesKey => settingsEntriesKey,
                settingsEntriesKey => CreateSettingsEntries(controlPanelRegistryItems, settingsEntriesKey.SettingsGroup.Position, settingsEntriesKey.SettingsCategory.Title)
            );

        if (preselectFirstSettingsEntryInFirstSettingsGroup)
        {
            if (_settingsGroups.Count < 1)
                return;

            var firstSettingsGroup = _settingsGroups.First();

            if (_settingsCategoryMaps.TryGetValue(firstSettingsGroup, out var settingsCategoryMap))
            {
                var firstSettingsCategory = settingsCategoryMap.Values.Sort().First();

                State.ExpandedSettingsCategory = firstSettingsCategory;

                var settingsEntriesKey = new SettingsEntriesKey { SettingsGroup = firstSettingsGroup, SettingsCategory = firstSettingsCategory };

                AdoptToCategoryExpand(settingsEntriesKey);
            }
        }
    }

    private IEnumerable<SettingsEntry> CreateSettingsEntries(IEnumerable<IControlPanelRegistryItem> controlPanelRegistryItems,
        int groupPosition, string categoryTitle)
    {
        var settingsEntries = SettingsService.GetSettingsEntries(controlPanelRegistryItems, groupPosition, categoryTitle);

        return settingsEntries.Sort();
    }

    private void AdoptRequestedSettingsEntry()
    {
        if (_requestedSettingsEntry is not null)
        {
            SetActiveControlPanel(_requestedSettingsEntry.ControlPanelRegistryItem);

            _requestedSettingsEntry = null;
        }
    }

    private Task ChildStateOnBeginOrCancelEdit()
    {
        LastSaveResult = null;

        // on event callbacks we need to call StateHasChanged explicit
        return InvokeAsync(StateHasChanged);
    }

    private async Task OnBeforeExpandAccordionItem(AccordionItemCancelEventArgs args, SettingsGroup settingsGroup)
    {
        if (args.Reason == NavigationItemStateChangeReason.ApiCall)
        {
            // Call originates from IControlPanelRequest<> or other code parts whereas it is expected
            // that these code parts adjust state as needed, therefore we simply return an do nothing.
            return;
        }

        var settingsCategoryTitle = args.ItemInfo.Text;

        if (!_settingsCategoryMaps.TryGetValue(settingsGroup, out var settingsCategoryMap))
            return;

        if (!settingsCategoryMap.TryGetValue(settingsCategoryTitle, out var settingsCategory))
            return;

        var settingsEntriesKey = new SettingsEntriesKey { SettingsGroup = settingsGroup, SettingsCategory = settingsCategory };

        if (ControlPanelService.IsDirty)
        {
            _requestedExpandSettingsCategory = settingsEntriesKey;
            await OnHasUnsavedChanges.InvokeAsync();
        }
        else
        {
            AdoptToCategoryExpand(settingsEntriesKey);
        }
    }

    private void AdoptToCategoryExpand(SettingsEntriesKey settingsEntriesKey)
    {
        if (State.LastActiveControlPanelRegistryItemMap.TryGetValue(settingsEntriesKey, out var lastActiveControlPanelRegistryItem))
        {
            SetActiveControlPanel(lastActiveControlPanelRegistryItem);
        }
        else
        {
            // Try to make the first control panel in settingsCategory the active control panel

            if (_settingsEntriesMap.TryGetValue(settingsEntriesKey.SettingsGroup, settingsEntriesKey.SettingsCategory, out var settingsEntries))
            {
                var firstSettingsEntry = settingsEntries.FirstOrDefault();
                if (firstSettingsEntry is not null)
                    SetActiveControlPanel(firstSettingsEntry.ControlPanelRegistryItem);
            }
        }
    }

    private void OnBeforeCollapseAccordionItem(AccordionItemCancelEventArgs args, SettingsGroup settingsGroup)
    {
        if (args.Reason == NavigationItemStateChangeReason.ApiCall)
        {
            // Call originates from IControlPanelRequest<> or other code parts whereas it is expected
            // that these code parts adjust state as needed, therefore we simply return an do nothing.
            return;
        }

        var settingsEntriesKey = GetSettingsEntriesKey(args, settingsGroup);
        if (settingsEntriesKey is null)
            return;

        // prevent collapse of expanded category
        if (State.ExpandedSettingsCategory == settingsEntriesKey.SettingsCategory &&
            State.ExpandedSettingsCategory?.GroupPosition == settingsEntriesKey.SettingsGroup.Position)
        {
            args.Cancel = true;
        }
    }

    private SettingsEntriesKey? GetSettingsEntriesKey(AccordionItemStateChangeEventArgs args, SettingsGroup settingsGroup)
    {
        var settingsCategoryTitle = args.ItemInfo.Text;

        if (!_settingsCategoryMaps.TryGetValue(settingsGroup, out var settingsCategoryMap))
            return null;

        if (!settingsCategoryMap.TryGetValue(settingsCategoryTitle, out var settingsCategory))
            return null;

        var settingsEntriesKey = new SettingsEntriesKey { SettingsGroup = settingsGroup, SettingsCategory = settingsCategory };

        return settingsEntriesKey;
    }

    private async Task OnSettingsEntryClick(SettingsEntry settingsEntry)
    {
        if (ControlPanelService.IsDirty)
        {
            _requestedSettingsEntry = settingsEntry;
            await OnHasUnsavedChanges.InvokeAsync();
        }
        else
        {
            SetActiveControlPanel(settingsEntry.ControlPanelRegistryItem);
        }
    }

    private async Task PopupCloseActionButtonClick()
    {
        if (OnClose.HasDelegate)
            await OnClose.InvokeAsync();
    }

    private static void ResetActiveControlPanelPage(IControlPanelRegistryItem? controlPanelRegistryItem)
    {
        if (controlPanelRegistryItem is not null)
            controlPanelRegistryItem.State.ActivePageIndex = null;
    }

    private async Task RevertChildState()
    {
        Logger.LogDebug("RevertChildState called");

        // lock against already running operation
        if (State.IsLoadingOverlayVisible)
            return;

        // open loading spinner and refresh component to show the change
        State.IsLoadingOverlayVisible = true;

        try
        {
            await ControlPanelService.CancelEdit();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unexpected error occurred while requesting cancel edit");

            LastSaveResult = new ControlPanelStateFinishResult(false,
                string.Format(CultureInfo.CurrentCulture, Localization.SettingsContainer.UnexpectedErrorWhileCancelEdit, e.Message));

            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            // close loading spinner and refresh component to show the change
            State.IsLoadingOverlayVisible = false;
        }
    }

    private async Task SaveChildState()
    {
        // todo: if error on form was fixed and then save gets clicked afterwards it does not trigger this call !?!
        Logger.LogDebug("SaveChildState called");

        // lock against already running operation
        if (State.IsLoadingOverlayVisible)
            return;

        // open loading spinner and refresh component to show the change
        State.IsLoadingOverlayVisible = true;
        try
        {
            // on click events the change detection is triggered automatically
            LastSaveResult = await ControlPanelService.FinishEdit();

            if (LastSaveResult.Success)
                await Update();
        }
        finally
        {
            State.IsLoadingOverlayVisible = false;
        }
    }

    private void SetActiveControlPanel(IControlPanelRegistryItem? controlPanelRegistryItem)
    {
        State.BeginUpdate();
        try
        {
            var settingsEntriesKey = GetSettingsEntriesKey(controlPanelRegistryItem);

            ResetActiveControlPanelPage(State.ActiveControlPanelRegistryItem);

            State.ActiveControlPanelRegistryItem = controlPanelRegistryItem;

            State.ClearRequestedControlPanelRegistryItems();
            if (controlPanelRegistryItem is not null)
                State.TryPushRequestedControlPanelRegistryItem(controlPanelRegistryItem);

            State.ShowNavigateBackButton = false;

            ResetActiveControlPanelPage(State.ActiveControlPanelRegistryItem);
        }
        finally
        {
            State.EndUpdate();
        }
    }

    private void UpdateLastActiveControlPanelRegistryItem(SettingsEntriesKey settingsEntriesKey, IControlPanelRegistryItem? controlPanelRegistryItem)
    {
        if (controlPanelRegistryItem is not null)
            State.AddOrSetLastActiveControlPanelRegistryItem(settingsEntriesKey, controlPanelRegistryItem);
        else
            State.RemoveLastActiveControlPanelRegistryItem(settingsEntriesKey);
    }

    private async Task ControlPanelRequested(ControlPanelRequestedEventArgs args)
    {
        if (ControlPanelService.IsDirty)
        {
            args.Cancel = true;

            _requestedControlPanelRequestedEventArgs = args;
            await OnHasUnsavedChanges.InvokeAsync();
        }
    }

    private async Task NavigateBackRequested(NavigateBackRequestedEventArgs args)
    {
        if (ControlPanelService.IsDirty)
        {
            args.Cancel = true;

            _requestedNavigateBack = true;
            await OnHasUnsavedChanges.InvokeAsync();
        }
    }
}
