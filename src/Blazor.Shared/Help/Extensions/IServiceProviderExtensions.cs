using Blazor.Shared.Help.NotificationArea;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.Help.Extensions;

public static class IServiceProviderExtensions
{
    public static IServiceProvider UseHelp(this IServiceProvider services)
    {
#if DEBUG
        var NotificationElementState = new NotificationElementState();

        var notificationElementRegistry = services.GetRequiredService<INotificationElementRegistry<SharedClientModule>>();
        notificationElementRegistry.Add<HelpNotificationElement, NotificationElementState>(NotificationElementState, position: 1);
#endif

        return services;
    }
}
