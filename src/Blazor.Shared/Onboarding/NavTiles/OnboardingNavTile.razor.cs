using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Components;

namespace Blazor.Shared.Onboarding.NavTiles;

public partial class OnboardingNavTile : NavTileBase
{
    public const string Id = "54a7f368-6e52-4560-8221-91ea72f056ef";

    private readonly Uri _iconUrl = GetIconUrl();

    private static Uri GetIconUrl()
        => ModuleAssetHelper.GetModuleImageUrl<SharedClientModule>($"onboarding-navtile/icon.svg");
}
