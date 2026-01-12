using Sdk.Client.NavTiles.Attributes;
using Sdk.Client.NavTiles.Components;

namespace TestModule.Client.NavTiles
{
    [InitialNavTile<TestSomeEditorClientModule>(LinkTarget = "/some-editor")]
    public partial class SomeEditorNavTile : NavTileBase
    {
    }
}
