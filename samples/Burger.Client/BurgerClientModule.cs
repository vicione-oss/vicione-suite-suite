using Burger.Client.Connections;
using Burger.Client.ControlPanels.Extensions;
using Burger.Client.Localization;
using Burger.Client.Services;
using Burger.Internal;
using Burger.Public.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Connections;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.Services;

namespace Burger.Client;

public sealed class BurgerClientModule : ClientModule
{
    public const string ModuleRoute = "/burger";

    public override Action<IServiceCollection> Configure => (services) =>
    {
        services.AddScoped<OrderBurgerService>();
        services.AddTransient<IClientModuleResourceProvider, BurgerClientResourceProvider>();

        services.AddLocalization<BurgerClientModule, Localizer>();
        services.AddNavTiles<BurgerClientModule>();
        services.AddControlPanels();
    };

    public override Func<IServiceProvider, Task>? InitializeServices => (services) =>
    {
        services.GetRequiredService<IConnectionTypeUiRegistry>()
        .Register<BurgerConnection, BurgerSettings, BurgerItemValidator>(Constants.BurgerConnectionType, () => Common.ModuleTitle);

        return Task.CompletedTask;
    };
}
