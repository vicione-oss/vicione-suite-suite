using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;
using Sdk.Client.NavTiles.Enums;

namespace TestModule.Client.NavTiles
{
    [InitialNavTile<TestOtherEditorClientModule>(LinkTarget = "/other-editor", Group = NavTileGroup.Administration)]
    public partial class OtherEditorNavTile : NavTileBase
    {
    }
}
