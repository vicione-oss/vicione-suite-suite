using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Components;

namespace Blazor.Shared.Onboarding.NavTiles;

public partial class OnboardingNavTile : NavTileBase
{
    public const string Id = "54a7f368-6e52-4560-8221-91ea72f056ef";

    private readonly string _iconSrc = GetIconSrc();

    private static string GetIconSrc()
        => ModuleAssetHelper.GetModuleImagePath<SharedClientModule>($"onboarding-navtile/icon.svg");
}
