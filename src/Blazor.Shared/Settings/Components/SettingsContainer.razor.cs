using System.Collections.Concurrent;
using System.Globalization;
using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.Popup.Factories;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Models.Actions;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.Accordion.Models;
using ViciOne.Ui.Blazor.Components.LoadingSpinner.Models;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsContainer : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1);

    private List<SettingsGroup> _settingsGroups = [];
    private readonly ConcurrentDictionary<SettingsGroup, Dictionary<string, SettingsCategory>> _settingsCategoryMaps = [];
    private Dictionary<SettingsEntriesKey, IEnumerable<SettingsEntry>> _settingsEntriesMap = [];

    private IAction? _deferredAction;
    private readonly Lock _deferredActionLock = new();

    private SettingsEntry? _activeSettingsEntry;

    private List<TimedMessage>? _loadingSpinnerTimedMessages;

    private bool _initialized;
    private readonly List<SaveErrorResult> _lastSaveErrorResults = [];
    private int _controlPanelEditRunningCount;
    private readonly Lock _controlPanelEditRunningCountLock = new();

    private CancellationToken CancellationToken => _cancellationTokenSource.Token;

    private List<TimedMessage> LoadingSpinnerTimedMessages => _loadingSpinnerTimedMessages ??= [.. TimedMessagesFactory.CreateTimedMessages()];

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback OnShowUnsavedChangesPopup { get; set; }

    [Inject] private IControlPanelEditRegistry ControlPanelEditRegistry { get; set; } = default!;
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

        ControlPanelEditRegistry.Changed -= ControlPanelEditRegistryChanged;

        foreach (var controlPanelEdit in ControlPanelEditRegistry)
            controlPanelEdit.Changed -= ControlPanelEditChanged;

        NavigateBackRequest.NavigateBackRequested -= NavigateBackRequested;
        ControlPanelRequest.ControlPanelRequested -= ControlPanelRequested;

        ControlPanelRegistryItemCache.Changed -= ControlPanelRegistryItemCacheChanged;

        LoadingIndicationPlacementBehavior.PlacementChanged -= LoadingIndicationPlacementChanged;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        _updateSemaphore.Dispose();
    }

    private void CancelDeferredAction()
    {
        lock (_deferredActionLock)
            _deferredAction = null;
    }

    private async Task ExecuteDeferredAction()
    {
        IAction? deferredAction;

        lock (_deferredActionLock)
        {
            deferredAction = _deferredAction;

            _deferredAction = null;
        }

        if (deferredAction is ControlPanelRequestAction controlPanelRequestAction)
            await ControlPanelRequest.Send(controlPanelRequestAction.Args.RegistryItem, controlPanelRequestAction.Args.ConfigureState);

        else if (deferredAction is NavigateBackAction)
            await NavigateBackRequest.Send();

        else if (deferredAction is ExpandSettingsCategoryAction expandSettingsCategoryAction)
            AdoptToCategoryExpand(expandSettingsCategoryAction.SettingsEntriesKey);

        else if (deferredAction is SelectSettingsEntryAction selectSettingsEntryAction)
            SetActiveControlPanel(selectSettingsEntryAction.SettingsEntry.ControlPanelRegistryItem);

        else if (deferredAction is CloseAction)
            await InvokeOnClose();
    }

    internal void OnCancelUnsavedChangesPopup()
        => CancelDeferredAction();

    internal async Task OnRevertUnsavedChangesPopup()
    {
        var success = await CancelControlPanelEdits();

        if (success)
            await ExecuteDeferredAction();
        else
            CancelDeferredAction();
    }

    internal async Task OnSaveUnsavedChangesPopup()
    {
        var success = await SaveControlPanelEdits();

        if (success)
        {
            await ExecuteDeferredAction();

            await Update();
        }
        else
        {
            CancelDeferredAction();
        }
    }

    private async Task ConfirmButtonClick()
    {
        var success = await SaveControlPanelEdits();

        if (success)
            await Update();
    }

    private async Task<IEnumerable<IControlPanelRegistryItem>> GetControlPanelRegistryItem(CancellationToken cancellationToken)
    {
        var user = await AuthenticationStateProvider.GetUser();

        var controlPanelRegistryItems = await ControlPanelRegistryItemCache.GetAll(user, cancellationToken);

        return controlPanelRegistryItems;
    }

    protected override async Task OnInitializedAsync()
    {
        if (Interlocked.CompareExchange(ref _initialized, true, false))
            return;

        if (CancellationToken.IsCancellationRequested)
            return;

        var controlPanelRegistryItems = (await GetControlPanelRegistryItem(CancellationToken)).ToArray();

        UpdateAccordionData(controlPanelRegistryItems, preselectFirstSettingsEntryInFirstSettingsGroup: true);

        UpdateActiveControlPanel(controlPanelRegistryItems);
        UpdateActiveSettingsEntry();

        ControlPanelEditRegistry.Changed += ControlPanelEditRegistryChanged;

        foreach (var controlPanelEdit in ControlPanelEditRegistry)
            controlPanelEdit.Changed += ControlPanelEditChanged;

        State.Changed += StateChanged;

        ControlPanelRequest.ControlPanelRequested += ControlPanelRequested;
        NavigateBackRequest.NavigateBackRequested += NavigateBackRequested;

        ControlPanelRegistryItemCache.Changed += ControlPanelRegistryItemCacheChanged;

        LoadingIndicationPlacementBehavior.PlacementChanged += LoadingIndicationPlacementChanged;
    }

    private void ControlPanelEditRegistryChanged(RegistryChangedEventArgs<IControlPanelEdit> args)
    {
        foreach (var controlPanelEdit in args.ItemsRemoved)
            controlPanelEdit.Changed -= ControlPanelEditChanged;

        foreach (var controlPanelEdit in args.ItemsAdded)
            controlPanelEdit.Changed += ControlPanelEditChanged;
    }

    private async void ControlPanelRegistryItemCacheChanged()
    {
        CancelDeferredAction();

        try
        {
            await Update();
        }
        catch (ObjectDisposedException)
        {
            Logger.LogWarning("Update control panel registry caused ObjectDisposedException");
        }
    }

    private bool UpdateActiveControlPanel(IControlPanelRegistryItem[] controlPanelRegistryItems)
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
                    var settingsEntriesKey = new SettingsEntriesKey { SettingsGroup = firstSettingsGroup, SettingsCategory = firstSettingsCategory };

                    if (_settingsEntriesMap.TryGetValue(settingsEntriesKey, out var settingsEntries))
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
        try
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
            finally
            {
                _updateSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, we just return gracefully
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource behind CancellationToken already disposed, nothing we can do, return gracefully
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

    private async void ControlPanelEditChanged(PropertiesChangedEventArgs args)
    {
        if (args.Sender is IControlPanelEdit controlPanelEdit && args.PropertyNames.Contains(nameof(IControlPanelEdit.IsRunning)))
        {
            lock (_controlPanelEditRunningCountLock)
            {
                if (controlPanelEdit.IsRunning)
                    _controlPanelEditRunningCount += 1;
                else
                    _controlPanelEditRunningCount -= 1;

                if (_controlPanelEditRunningCount < 0)
                    _controlPanelEditRunningCount = 0;
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnBeforeExpandAccordionItem(AccordionItemCancelEventArgs args, SettingsEntriesKey settingsEntriesKey)
    {
        if (IsAnyControlPanelEditRunning())
        {
            args.Cancel = true;

            lock (_deferredActionLock)
                _deferredAction = new ExpandSettingsCategoryAction(settingsEntriesKey);

            await OnShowUnsavedChangesPopup.InvokeAsync();
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

            if (_settingsEntriesMap.TryGetValue(settingsEntriesKey, out var settingsEntries))
            {
                var firstSettingsEntry = settingsEntries.FirstOrDefault();
                if (firstSettingsEntry is not null)
                    SetActiveControlPanel(firstSettingsEntry.ControlPanelRegistryItem);
            }
        }
    }

    private void OnBeforeCollapseAccordionItem(AccordionItemCancelEventArgs args, SettingsEntriesKey settingsEntriesKey)
    {
        // prevent collapse of expanded category
        if (State.ExpandedSettingsCategory == settingsEntriesKey.SettingsCategory &&
            State.ExpandedSettingsCategory?.GroupPosition == settingsEntriesKey.SettingsGroup.Position)
        {
            args.Cancel = true;
        }
    }

    private async Task OnSettingsEntryClick(SettingsEntry settingsEntry)
    {
        if (IsAnyControlPanelEditRunning())
        {
            lock (_deferredActionLock)
                _deferredAction = new SelectSettingsEntryAction(settingsEntry);

            await OnShowUnsavedChangesPopup.InvokeAsync();
        }
        else
        {
            SetActiveControlPanel(settingsEntry.ControlPanelRegistryItem);
        }
    }

    private async Task PopupCloseActionButtonClick()
    {
        if (IsAnyControlPanelEditRunning())
        {
            lock (_deferredActionLock)
                _deferredAction = new CloseAction();

            await OnShowUnsavedChangesPopup.InvokeAsync();
        }
        else
        {
            await InvokeOnClose();
        }
    }

    private async Task InvokeOnClose()
    {
        if (OnClose.HasDelegate)
            await OnClose.InvokeAsync();
    }

    private static void ResetActiveControlPanelPage(IControlPanelRegistryItem? controlPanelRegistryItem)
    {
        if (controlPanelRegistryItem is not null)
            controlPanelRegistryItem.State.ActivePageIndex = null;
    }

    private async Task<bool> CancelControlPanelEdits()
    {
        Logger.LogDebug("CancelControlPanelEdits called");

        State.IsLoadingOverlayVisible = true;
        try
        {
            _lastSaveErrorResults.Clear();

            var controlPanelEdits = ControlPanelEditRegistry.ToArray();
            var cancelTasks = controlPanelEdits.Select(controlPanelEdit => controlPanelEdit.Cancel(withReset: true)).ToArray();

            var cancelResults = await Task.WhenAll(cancelTasks);

            var success = cancelResults.All(cancelResult => cancelResult);

            return success;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unexpected error occurred while cancelling edits");

            _lastSaveErrorResults.Add(new SaveErrorResult(string.Format(CultureInfo.CurrentCulture,
                Localization.SettingsContainer.UnexpectedErrorWhileCancellingEdits, e.Message)));

            return false;
        }
        finally
        {
            State.IsLoadingOverlayVisible = false;
        }
    }

    private async Task<bool> SaveControlPanelEdits()
    {
        Logger.LogDebug("SaveControlPanelEdits called");

        State.IsLoadingOverlayVisible = true;
        try
        {
            _lastSaveErrorResults.Clear();

            var controlPanelEdits = ControlPanelEditRegistry.ToArray();
            var saveTasks = controlPanelEdits.Select(controlPanelEdit => controlPanelEdit.Save()).ToArray();

            var saveResults = await Task.WhenAll(saveTasks);

            _lastSaveErrorResults.AddRange(saveResults.OfType<SaveErrorResult>());

            var success = _lastSaveErrorResults.Count == 0;

            return success;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unexpected error occurred while saving edits");

            _lastSaveErrorResults.Add(new SaveErrorResult(string.Format(CultureInfo.CurrentCulture,
                Localization.SettingsContainer.UnexpectedErrorWhileSavingEdits, e.Message)));

            return false;
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
        if (IsAnyControlPanelEditRunning())
        {
            args.Cancel = true;

            lock (_deferredActionLock)
                _deferredAction = new ControlPanelRequestAction(args);

            await OnShowUnsavedChangesPopup.InvokeAsync();
        }
    }

    private async Task NavigateBackRequested(NavigateBackRequestedEventArgs args)
    {
        if (IsAnyControlPanelEditRunning())
        {
            args.Cancel = true;

            lock (_deferredActionLock)
                _deferredAction = new NavigateBackAction();

            await OnShowUnsavedChangesPopup.InvokeAsync();
        }
    }

    private bool IsAnyControlPanelEditRunning()
    {
        lock (_controlPanelEditRunningCountLock)
            return _controlPanelEditRunningCount > 0;
    }
}
