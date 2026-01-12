using Microsoft.AspNetCore.Components;
using Sdk.Client.NavTiles.Components;

namespace Blazor.Shared.NavTiles.Components;

public sealed partial class NavTileContainer : ComponentBase, IDisposable
{
    private DynamicComponent? _navTile;

    [Parameter, EditorRequired] public Type NavTileComponentType { get; set; }
    [Parameter, EditorRequired] public NavTileState NavTileState { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        NavTileState.Changed -= NavTileStateChanged;
        NavTileState.Changed += NavTileStateChanged;
    }
    public void Dispose()
    {
        NavTileState.Changed -= NavTileStateChanged;

        GC.SuppressFinalize(this);
    }

    private async void NavTileStateChanged() => await InvokeAsync(StateHasChanged);

    private void LinkClicked()
    {
        if (!NavTileState.Enabled)
            return;

        if (_navTile is not null)
            (_navTile.Instance as NavTileBase)?.Click();
    }
}
