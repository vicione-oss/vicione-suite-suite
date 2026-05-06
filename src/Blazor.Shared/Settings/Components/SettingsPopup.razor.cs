using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Models;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsPopup : ComponentBase, IDisposable
{
    private SettingsContainer? _settingsContainer;
    private bool _showUnsavedChangesPopup;

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] private ISettingsPopupRequest SettingsPopupRequest { get; set; } = default!;
    [Inject] private ISettingsPopupState SettingsPopupState { get; set; } = default!;
    [Inject] private SettingsModuleState SettingsModuleState { get; set; } = default!;
    [Inject] private ILogger<SettingsPopup> Logger { get; set; } = default!;

    protected override void OnInitialized()
    {
        ControlPanelRequest.ControlPanelRequested += ControlPanelRequested;
        SettingsPopupRequest.SettingsPopupRequested += SettingsPopupRequested;
        SettingsPopupState.Changed += SettingsPopupStateChanged;
    }

    public void Dispose()
    {
        SettingsPopupState.Changed -= SettingsPopupStateChanged;
        SettingsPopupRequest.SettingsPopupRequested -= SettingsPopupRequested;
        ControlPanelRequest.ControlPanelRequested -= ControlPanelRequested;
    }

    private async Task ControlPanelRequested(ControlPanelRequestedEventArgs args)
    {
        if (SettingsPopupState.Visible)
        {
            // Do nothing as SettingsContainer and ControlPanelCarousel already listen to the request
        }
        else
        {
            args.ConfigureState?.Invoke();

            SettingsModuleState.AdoptToControlPanelRequest(args, Logger);

            SettingsPopupState.Visible = true;
        }
    }

    private void SettingsPopupRequested()
    {
        SettingsModuleState.PreselectFirstSettingsEntryInFirstSettingsGroup = true;

        SettingsPopupState.Visible = true;
    }

    private void SettingsPopupStateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(SettingsPopupState.Visible)))
            InvokeAsync(StateHasChanged);
    }

    private void OnCancelUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;
        _settingsContainer?.OnCancelUnsavedChangesPopup();
    }

    private void SettingsContainerOnClose()
        => SettingsPopupState.Visible = false;

    private async Task OnRevertUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;

        if (_settingsContainer is not null)
            await _settingsContainer.OnRevertUnsavedChangesPopup();
    }

    private async Task OnSaveUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;

        if (_settingsContainer is not null)
            await _settingsContainer.OnSaveUnsavedChangesPopup();
    }
}
