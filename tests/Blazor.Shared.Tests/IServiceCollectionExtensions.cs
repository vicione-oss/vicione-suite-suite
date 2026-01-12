using Blazor.Shared.Settings.Services;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection SetupControlPanelRegistry(this IServiceCollection services, Action<IControlPanelRegistryItem<SharedClientModule>>? registryItemSetup)
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

        services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());
        services.AddSingleton(Substitute.For<IControlPanelPageRegistry>());

        return services;
    }

    public static IServiceCollection SetupControlPanelRegistryCache(this IServiceCollection services)
    {
        services.AddScoped(services =>
        {
            // resolve items registered by SetupControlPanelRegistry()
            var registryItems = services.GetRequiredService<IEnumerable<IControlPanelRegistryItem<SharedClientModule>>>();

            var result = Substitute.For<IControlPanelRegistryItemCache>();

            result.GetAll(Arg.Any<ClaimsPrincipal?>(), Arg.Any<CancellationToken>()).Returns(registryItems);

            return result;
        });

        return services;
    }
}
