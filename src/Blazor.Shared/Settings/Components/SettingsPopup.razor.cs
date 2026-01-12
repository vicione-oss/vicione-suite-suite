using Blazor.Shared.Settings.Services;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsPopup : ComponentBase, IDisposable
{
    private DxPopup? _dxPopup;
    private SettingsContainer? _settingsContainer;
    private bool _showUnsavedChangesPopup;

    [Inject] private IControlPanelService ChildService { get; set; } = default!;
    [Inject] private ISettingsPopupRequest SettingsPopupRequest { get; set; } = default!;
    [Inject] private ISettingsPopupState SettingsPopupState { get; set; } = default!;

    protected override void OnInitialized()
        => SettingsPopupRequest.SettingsPopupRequested += SettingsPopupRequested;

    public void Dispose()
        => SettingsPopupRequest.SettingsPopupRequested -= SettingsPopupRequested;

    private async Task SettingsPopupRequested()
    {
        SettingsPopupState.Visible = true;

        await InvokeAsync(StateHasChanged);
    }

    private void OnCancelUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;
        _settingsContainer?.OnCancelUnsavedChangesPopup();
    }

    private async Task SettingsContainerOnClose()
    {
        if (_dxPopup is not null)
            await _dxPopup.CloseAsync();
    }

    private void OnClosing(PopupClosingEventArgs args)
    {
        if (ChildService.IsDirty)
        {
            _showUnsavedChangesPopup = true;
            args.Cancel = true;
        }
    }

    private async Task OnRevertUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;

        if (_settingsContainer is not null)
            await _settingsContainer.OnRevertUnsavedChangesPopup();

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSaveUnsavedChanges()
    {
        if (_settingsContainer is not null)
            await _settingsContainer.OnSaveUnsavedChangesPopup();

        _showUnsavedChangesPopup = false;

        await InvokeAsync(StateHasChanged);
    }
}
