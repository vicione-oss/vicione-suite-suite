using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;

namespace TestModule.Client.NavTiles
{
    [InitialNavTile<TestClientModule>(LinkTarget = TestClientModule.ModuleRoute)]
    public partial class TestClientModuleNavTile : NavTileBase
    {
    }
}
