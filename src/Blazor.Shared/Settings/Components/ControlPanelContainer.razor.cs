using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Factories;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Components;

public sealed partial class ControlPanelContainer : ComponentBase, IAsyncDisposable
{
    private bool _disposedAsync;

    private readonly Dictionary<string, object> _controlPanelParameters = [];

    private IControlPanelState? _controlPanelState;
    private IControlPanelEdit? _controlPanelEdit;

    [Parameter, EditorRequired] public IControlPanelRegistryItem ControlPanelRegistryItem { get; set; }
    [Parameter, EditorRequired] public ControlPanelLoadingIndicationSectionId LoadingIndicationSectionId { get; set; }

    [Inject] private IControlPanelEditRegistry ControlPanelEditRegistry { get; set; } = default!;
    [Inject] private ControlPanelEditFactory ControlPanelEditFactory { get; set; } = default!;

    protected override void OnInitialized()
    {
        _controlPanelState = ControlPanelRegistryItem.State;
        _controlPanelState.Changed += ControlPanelStateChanged;

        _controlPanelParameters.Add(nameof(ControlPanelBase<IControlPanelState>.State), _controlPanelState);

        _controlPanelParameters.Add(nameof(ControlPanelBase<IControlPanelState>.OnBeginEdit),
            EventCallback.Factory.Create(this, ControlPanelBeginEdit));

        _controlPanelParameters.Add(nameof(ControlPanelBase<IControlPanelState>.OnCancelEdit),
            EventCallback.Factory.Create(this, ControlPanelCancelEdit));

        _controlPanelEdit = ControlPanelEditFactory.CreateControlPanelEdit(_controlPanelState);
        _controlPanelEdit.Changed += ControlPanelEditChanged;

        ControlPanelEditRegistry.Add(_controlPanelEdit);
    }

    private void ControlPanelEditChanged(Sdk.Client.Models.PropertiesChangedEventArgs obj)
        // Re-render ourself because the re-render implemented in SettingsContainer does not reach us due to missing ControlPanelCarousel parameters
        => InvokeAsync(StateHasChanged);

    protected override async Task OnInitializedAsync()
    {
        if (_disposedAsync)
            return;

        if (_controlPanelEdit is not null)
            await _controlPanelEdit.Reset();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposedAsync, true, false))
            return;

        // For now, we do not call CancelEdit because it would require an await on the async behavior
        // which could result in memory leaks when the await never returns

        if (_controlPanelState is not null)
            _controlPanelState.Changed -= ControlPanelStateChanged;

        if (_controlPanelEdit is not null)
        {
            _controlPanelEdit.Changed -= ControlPanelEditChanged;

            ControlPanelEditRegistry.Remove(_controlPanelEdit);

            await _controlPanelEdit.DisposeAsync();
        }
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
