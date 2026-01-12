using Microsoft.AspNetCore.Authorization;
using Sdk.Client.NavTiles.Components;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.NavTiles.Services;

internal sealed class NavTileRegistryItem : INavTileRegistryItem
{
    public required string Id { get; init; }
    public required Type ComponentType { get; init; }
    public required NavTileState State { get; init; }
    public required NavTileGroup Group { get; init; }
    public IAuthorizationRequirement? AuthorizationRequirement { get; init; }
}
