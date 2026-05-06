using Blazor.Shared.UserManagement.ControlPanels.Users.Components;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using Sdk.Client.NavTiles.Components;

namespace Blazor.Shared.UserManagement.NavTiles;

public sealed partial class ManageUsersNavTile : NavTileBase
{
    public const string Id = "e133e04c-a6b0-4510-88d2-e77dd1db08be";

    private readonly Uri _iconUrl = GetIconUrl();

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;

    public override void Click()
        => ControlPanelRequest.Send<UsersControlPanel>();

    private static Uri GetIconUrl()
        => ModuleAssetHelper.GetModuleIconUrl<SharedClientModule>("user.svg");
}
