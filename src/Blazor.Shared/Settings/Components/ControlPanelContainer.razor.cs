using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Components;

public sealed partial class ControlPanelContainer : ComponentBase, IDisposable
{
    private IControlPanelState _controlPanelState = default!;

    [Parameter, EditorRequired] public IControlPanelRegistryItem ControlPanelRegistryItem { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (_controlPanelState != ControlPanelRegistryItem.State)
        {
            if (_controlPanelState is not null)
                _controlPanelState.Changed -= ControlPanelStateChanged;

            _controlPanelState = ControlPanelRegistryItem.State;

            _controlPanelState.Changed += ControlPanelStateChanged;
        }
    }

    public void Dispose()
    {
        if (_controlPanelState is not null)
            _controlPanelState.Changed -= ControlPanelStateChanged;
    }

    private async void ControlPanelStateChanged(ControlPanelStateChangedEventArgs args)
        => await InvokeAsync(StateHasChanged);
}
