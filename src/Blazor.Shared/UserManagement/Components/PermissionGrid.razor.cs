using Blazor.Shared.UserManagement.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.UserManagement.Components;

public sealed partial class PermissionGrid
{
    [Parameter, EditorRequired]
    public required IQueryable<PermissionGridItem> Items { get; set; }

    [Parameter, EditorRequired]
    public required string? Filter { get; set; }

    [Parameter]
    public EventCallback<string?> FilterChanged { get; set; }

    [Parameter, EditorRequired]
    public required EventCallback<PermissionGridItem> AccessLevelChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }
}
