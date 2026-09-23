using Blazor.Shared.Settings.Services;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection SetupControlPanelRegistry(Action<IControlPanelRegistryItem<SharedClientModule>>? registryItemSetup)
        {
            services.AddScoped(serviceProvider =>
            {
                var registryItem = Substitute.For<IControlPanelRegistryItem<SharedClientModule>>();

                registryItemSetup?.Invoke(registryItem);

                return registryItem;
            });

            services.AddScoped<IControlPanelRegistry>(serviceProvider =>
            {
                var registryItems = serviceProvider.GetRequiredService<IEnumerable<IControlPanelRegistryItem<SharedClientModule>>>();
                var registry = Substitute.For<IControlPanelRegistry<SharedClientModule>>();

                registry.GetEnumerator().Returns(callInfo => registryItems.GetEnumerator());

                return registry;
            });

            return services;
        }

        public IServiceCollection SetupControlPanelRegistryCache()
        {
            services.AddScoped(services =>
            {
                // Resolves the items registered by SetupControlPanelRegistry().
                var registryItems = services.GetRequiredService<IEnumerable<IControlPanelRegistryItem<SharedClientModule>>>();

                var result = Substitute.For<IControlPanelRegistryItemCache>();

                result.GetAll(Arg.Any<ClaimsPrincipal?>(), Arg.Any<CancellationToken>()).Returns(registryItems);

                return result;
            });

            return services;
        }
    }
}
