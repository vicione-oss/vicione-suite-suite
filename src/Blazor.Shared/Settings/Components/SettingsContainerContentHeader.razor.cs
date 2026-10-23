using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.TabStrip.Models;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsContainerContentHeader : ComponentBase, IDisposable
{
    private IControlPanelRegistryItem? _activeControlPanelRegistryItem;

    [Inject] private IEnumerable<IControlPanelRegistry> ControlPanelRegistries { get; set; } = default!;

    [Inject] private IActiveControlPanelChildProvider ActiveControlPanelChildProvider { get; set; } = default!;

    [Inject] private IControlPanelPageRegistry ControlPanelPageRegistry { get; set; } = default!;

    [Inject] private IActiveControlPanelPageProvider ActiveControlPanelPageProvider { get; set; } = default!;

    [Inject] private SettingsModuleState SettingsModuleState { get; set; } = default!;

    [Inject] private INavigateBackRequest NavigateBackRequest { get; set; } = default!;

    [Inject] private ILogger<SettingsContainerContentHeader> Logger { get; set; } = default!;

    /// <summary>
    /// Whether tab #0 shows the active item's own content; an item without content of its own only lists its children.
    /// </summary>
    private bool ActiveItemHasOwnTab => _activeControlPanelRegistryItem?.Descriptor.HasContent ?? true;

    protected override void OnInitialized()
    {
        foreach (var controlPanelRegistry in ControlPanelRegistries)
            controlPanelRegistry.Changed += ControlPanelRegistryChanged;

        ControlPanelPageRegistry.Changed += ControlPanelPageRegistryChanged;
        SettingsModuleState.Changed += SettingsModuleStateChanged;

        SetActiveControlPanelRegistryItem(SettingsModuleState.ActiveControlPanelRegistryItem);
    }

    public void Dispose()
    {
        SetActiveControlPanelRegistryItem(null);

        SettingsModuleState.Changed -= SettingsModuleStateChanged;
        ControlPanelPageRegistry.Changed -= ControlPanelPageRegistryChanged;

        foreach (var controlPanelRegistry in ControlPanelRegistries)
            controlPanelRegistry.Changed -= ControlPanelRegistryChanged;
    }

    private void ControlPanelRegistryChanged(RegistryChangedEventArgs<IControlPanelRegistryItem> _)
        => StateHasChanged();

    private void ControlPanelPageRegistryChanged(RegistryChangedEventArgs<IControlPanelPageRegistryItem> _)
        => StateHasChanged();

    private List<IControlPanelRegistryItem> GetChildren(IControlPanelRegistryItem parent)
        => ControlPanelRegistries.GetChildren(parent);

    /// <remarks>
    /// <c>TabStrip</c> only applies a changed <c>ActiveTabIndex</c> when its tabs change, so the markup keys it by the active
    /// item: control panels with identical tab titles, e.g. two network interfaces, would otherwise keep the previous tab.
    /// </remarks>
    private int GetActiveTabIndex(List<IControlPanelRegistryItem> children)
    {
        var activeChild = _activeControlPanelRegistryItem is not null
            ? ActiveControlPanelChildProvider.GetActiveChild(_activeControlPanelRegistryItem)
            : null;

        if (activeChild is null)
            return 0;

        return children.IndexOf(activeChild) + (ActiveItemHasOwnTab ? 1 : 0);
    }

    /// <summary>
    /// Handles every tab; <c>args.TabIndex</c> is the tab's position across the whole strip, as laid out in the markup.
    /// </summary>
    private void ControlPanelTabSelected(TabSelectedEventArgs args)
    {
        if (_activeControlPanelRegistryItem is null)
            return;

        var childIndex = ActiveItemHasOwnTab ? args.TabIndex - 1 : args.TabIndex;

        if (childIndex < 0)
        {
            SettingsModuleState.SetActiveChild(_activeControlPanelRegistryItem, null);

            return;
        }

        var children = GetChildren(_activeControlPanelRegistryItem);

        if (childIndex < children.Count)
            SettingsModuleState.SetActiveChild(_activeControlPanelRegistryItem, children[childIndex]);
    }

    /// <summary>
    /// The deprecated <see cref="ControlPanelPage"/>s of the active item, which modules use until they migrate to child control panels.
    /// </summary>
    private List<IControlPanelPage> GetControlPanelPages()
        => ControlPanelPageRegistry
            .Where(i => i.ControlPanelRegistryItem == _activeControlPanelRegistryItem)
            .Select(i => i.ControlPanelPage)
            .ToList();

    private int GetActivePageIndex(List<IControlPanelPage> controlPanelPages)
    {
        var activeControlPanelPage = _activeControlPanelRegistryItem is not null
            ? ActiveControlPanelPageProvider.GetActiveControlPanelPage(_activeControlPanelRegistryItem)
            : null;

        return activeControlPanelPage is not null ? Math.Max(controlPanelPages.IndexOf(activeControlPanelPage), 0) : 0;
    }

    private void ControlPanelPageTabSelected(TabSelectedEventArgs args)
        => _activeControlPanelRegistryItem?.State.ActivePageIndex = args.TabIndex;

    private async void SettingsModuleStateChanged(PropertiesChangedEventArgs args)
    {
        var shouldRender = false;

        if (args.PropertyNames.Contains(nameof(SettingsModuleState.ShowNavigateBackButton)))
            shouldRender = true;

        if (args.PropertyNames.Contains(nameof(SettingsModuleState.ActiveControlPanelRegistryItem)))
        {
            SetActiveControlPanelRegistryItem(SettingsModuleState.ActiveControlPanelRegistryItem);

            shouldRender = true;
        }

        if (args.PropertyNames.Contains(nameof(SettingsModuleState.ActiveChildMap)))
            shouldRender = true;

        if (shouldRender)
            await InvokeAsync(StateHasChanged);
    }

    private void SetActiveControlPanelRegistryItem(IControlPanelRegistryItem? activeControlPanelRegistryItem)
    {
        if (_activeControlPanelRegistryItem != activeControlPanelRegistryItem && _activeControlPanelRegistryItem is not null)
            _activeControlPanelRegistryItem.State.Changed -= ActiveControlPanelRegistryItemStateChanged;

        _activeControlPanelRegistryItem = activeControlPanelRegistryItem;

        _activeControlPanelRegistryItem?.State.Changed += ActiveControlPanelRegistryItemStateChanged;
    }

    private async void ActiveControlPanelRegistryItemStateChanged(ControlPanelStateChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(IControlPanelState.ActivePageIndex)))
            await InvokeAsync(StateHasChanged);
    }

    private async Task NavigateBackButtonClick()
        => await NavigateBackRequest.Send();

    private string GetControlPanelTitle(IControlPanelPage? controlPanelPage)
    {
        var fallbackTitle = CommonVocabulary.Unknown;

        try
        {
            if (!string.IsNullOrWhiteSpace(controlPanelPage?.Title))
                return controlPanelPage.Title;

            return _activeControlPanelRegistryItem?.Descriptor.Title ?? fallbackTitle;
        }
        catch (Exception ex)
        {
            GetControlPanelTitleFailed(Logger, ex);

            return fallbackTitle;
        }
    }

    [LoggerMessage(1, LogLevel.Error, "Get control panel title failed")]
    private static partial void GetControlPanelTitleFailed(ILogger<SettingsContainerContentHeader> logger, Exception exception);
}
