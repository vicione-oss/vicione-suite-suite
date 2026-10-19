using Blazor.Shared.Popup.ContentOverlays.Models;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Models;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Components;

public sealed partial class ControlPanelContainer : ComponentBase, IDisposable
{
    private bool _disposed;

    private readonly Dictionary<string, object> _controlPanelParameters = [];

    private IControlPanelState? _controlPanelState;

    private IControlPanelEdit? _controlPanelEdit;

    [Parameter, EditorRequired] public IControlPanelRegistryItem ControlPanelRegistryItem { get; set; }
    [Parameter, EditorRequired] public ContentOverlaySectionId LoadingIndicationSectionId { get; set; }

    /// <summary>
    /// Set by <see cref="ControlPanelCarousel"/> per top-level control panel visit. A tab switch remounts this container,
    /// so the edit and the reset bookkeeping live in the scope, where the control panel finds them again on its next mount.
    /// </summary>
    [CascadingParameter] private ControlPanelEditScope EditScope { get; set; } = default!;

    protected override void OnInitialized()
    {
        _controlPanelState = ControlPanelRegistryItem.State;
        _controlPanelState.Changed += ControlPanelStateChanged;

        _controlPanelParameters.Add(nameof(ControlPanelBase<IControlPanelState>.State), _controlPanelState);

        _controlPanelParameters.Add(nameof(ControlPanelBase<IControlPanelState>.OnBeginEdit),
            EventCallback.Factory.Create(this, ControlPanelBeginEdit));

        _controlPanelParameters.Add(nameof(ControlPanelBase<IControlPanelState>.OnCancelEdit),
            EventCallback.Factory.Create(this, ControlPanelCancelEdit));

        if (EditScope.TryGetOrCreateEdit(ControlPanelRegistryItem, out var controlPanelEdit))
        {
            _controlPanelEdit = controlPanelEdit;
            _controlPanelEdit.Changed += ControlPanelEditChanged;
        }
    }

    private void ControlPanelEditChanged(Sdk.Client.Models.PropertiesChangedEventArgs obj)
        // Re-render ourself because the re-render implemented in SettingsContainer does not reach us due to missing ControlPanelCarousel parameters
        => InvokeAsync(StateHasChanged);

    protected override async Task OnInitializedAsync()
    {
        if (_disposed)
            return;

        if (_controlPanelEdit is null || _controlPanelState is null)
            return;

        if (!EditScope.TryMarkReset(_controlPanelEdit))
            return;

        await _controlPanelEdit.Reset();
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, true, false))
            return;

        if (_controlPanelState is not null)
            _controlPanelState.Changed -= ControlPanelStateChanged;

        // The edit stays registered: EditScope owns it and ends it with the visit, not with the tab.
        if (_controlPanelEdit is not null)
            _controlPanelEdit.Changed -= ControlPanelEditChanged;
    }

    private async void ControlPanelStateChanged(ControlPanelStateChangedEventArgs args)
    {
        // No checking of certain status properties before re-rendering, as we do not know which changes to the status in derived panels should trigger re-rendering
        await InvokeAsync(StateHasChanged);
    }

    private void ControlPanelBeginEdit()
        => _controlPanelEdit?.Begin();

    private async Task ControlPanelCancelEdit()
    {
        if (_controlPanelEdit is null)
            return;

        await _controlPanelEdit.Cancel(withReset: true);
    }
}
