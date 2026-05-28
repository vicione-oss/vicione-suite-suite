using Burger.Client.ControlPanels.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Burger.Client.ControlPanels.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        internal IServiceCollection AddControlPanels()
        {
            services.AddControlPanel<BurgerClientModule, BurgerControlPanel, BurgerControlPanelState>()
                .WithAutoDiscovery<BurgerControlPanelDescriptor>()
                .WithSaveHandler<BurgerControlPanelSaveHandler>()
                .WithResetHandler<BurgerControlPanelResetHandler>();

            return services;
        }
    }
}
