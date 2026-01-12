using Microsoft.AspNetCore.Authorization;
using Sdk.Client.Modules;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.NotificationArea.Services;

internal sealed class NotificationElementRegistryItem<TClientModule>(Guid id, int position, Type componentType, INotificationElementState state, IAuthorizationRequirement? authorizationRequirement) :
    INotificationElementRegistryItem<TClientModule>
    where TClientModule : class, IClientModule
{
    public Guid Id { get; set; } = id;
    public int Position { get; } = position;
    public Type ComponentType { get; } = componentType;
    public INotificationElementState State { get; } = state;

    public IAuthorizationRequirement? AuthorizationRequirement { get; } = authorizationRequirement;
}
