using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;

namespace Burger.Client.NavTiles
{
    [InitialNavTile<BurgerClientModule>(LinkTarget = BurgerClientModule.ModuleRoute)]
    public partial class BurgerNavTile : NavTileBase
    {
    }
}
