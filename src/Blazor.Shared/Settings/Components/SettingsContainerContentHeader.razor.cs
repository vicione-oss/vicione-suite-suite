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

    [Inject] private IControlPanelPageRegistry ControlPanelPageRegistry { get; set; } = default!;

    [Inject] private IActiveControlPanelPageProvider ActiveControlPanelPageProvider { get; set; } = default!;

    [Inject] private SettingsModuleState SettingsModuleState { get; set; } = default!;

    [Inject] private INavigateBackRequest NavigateBackRequest { get; set; } = default!;

    [Inject] private ILogger<SettingsContainerContentHeader> Logger { get; set; } = default!;

    protected override void OnInitialized()
    {
        ControlPanelPageRegistry.Changed += ControlPanelPageRegistryChanged;
        SettingsModuleState.Changed += SettingsModuleStateChanged;

        SetActiveControlPanelRegistryItem(SettingsModuleState.ActiveControlPanelRegistryItem);
    }

    public void Dispose()
    {
        SetActiveControlPanelRegistryItem(null);

        SettingsModuleState.Changed -= SettingsModuleStateChanged;
        ControlPanelPageRegistry.Changed -= ControlPanelPageRegistryChanged;
    }

    private void ControlPanelPageRegistryChanged(RegistryChangedEventArgs<IControlPanelPageRegistryItem> _)
        => StateHasChanged();

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

        if (shouldRender)
            await InvokeAsync(StateHasChanged);
    }

    private void SetActiveControlPanelRegistryItem(IControlPanelRegistryItem? activeControlPanelRegistryItem)
    {
        if (_activeControlPanelRegistryItem != activeControlPanelRegistryItem && _activeControlPanelRegistryItem is not null)
            _activeControlPanelRegistryItem.State.Changed -= ActiveControlPanelRegistryItemStateChanged;

        _activeControlPanelRegistryItem = activeControlPanelRegistryItem;

        if (_activeControlPanelRegistryItem is not null)
            _activeControlPanelRegistryItem.State.Changed += ActiveControlPanelRegistryItemStateChanged;
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
