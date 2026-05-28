using Burger.Client.ControlPanels;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;

namespace Burger.Client.NavTiles;

[InitialNavTile<BurgerClientModule>()]
public sealed partial class ManageBurgerNavTile : NavTileBase
{
    private readonly Uri _iconUrl = GetIconUrl();

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    public override void Click()
        => ControlPanelRequest.Send<BurgerControlPanel>();

    private static Uri GetIconUrl()
        => ModuleAssetHelper.GetModuleIconUrl<BurgerClientModule>("burger.svg");
}
