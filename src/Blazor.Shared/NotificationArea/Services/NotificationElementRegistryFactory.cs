using Sdk.Client.Modules;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.NotificationArea.Services;

internal sealed class NotificationElementRegistryFactory : INotificationElementRegistryFactory
{
    public INotificationElementRegistry<TClientModule> CreateNotificationElementRegistry<TClientModule>() where TClientModule : class, IClientModule
        => new NotificationElementRegistry<TClientModule>();
}
