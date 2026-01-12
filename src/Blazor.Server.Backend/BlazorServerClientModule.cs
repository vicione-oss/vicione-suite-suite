using Blazor.Server.Backend.Localization;
using Blazor.Server.Backend.Services;
using Blazor.Shared;
using Blazor.Shared.Connections.Components;
using Blazor.Shared.Connections.Validators;
using Blazor.Shared.Extensions;
using Blazor.Shared.Help.Extensions;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Mqtt;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Client.Connections;
using Sdk.Client.Infrastructure;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;
using Sdk.Client.Services;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend;

public sealed class BlazorServerClientModule : ClientModule
{
    public override Action<IServiceCollection, HostingModel> ConfigureServices => (services, hostingModel) =>
    {
        if (hostingModel != HostingModel.BlazorServer)
            throw new InvalidOperationException();

        services.AddScoped<ClientTimeProvider>();
        services.AddScoped<IClientTimeProvider>(s => s.GetRequiredService<ClientTimeProvider>());
        services.AddKeyedScoped<TimeProvider, ClientTimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, (sp, _) => sp.GetRequiredService<ClientTimeProvider>());
        services.AddBlazorShared();

        // the special ones
        services
            .AddSingleton<IClientModuleService, BlazorServerModuleService>()
            .AddScoped<IUiMediator, BlazorServerUiMediator>()
            .AddTransient<IBackendLogService, BackendLogService>()
            .AddTransient<INavigationService, NavigationService>();

        // localization
        services.AddLocalization<BlazorServerClientModule, BlazorServerClientModuleLocalizer>();

        services.AddModuleFeature(_ => new ModuleFeature(Sdk.Constants.SystemModuleId, Constants.LogViewFeature, "Controls access to the 'Log viewer'-feature"));

        services.AddSingleton<IConnectionTypeUiRegistry, ConnectionTypeUiRegistry>(s =>
        {
            ConnectionTypeUiRegistry registry = new();

            registry.Register<MqttConnection, MqttSettings, MqttItemValidator>(ConnectionType.Mqtt, () => TechnicalAcronyms.Mqtt);
            registry.Register<HttpConnection, HttpSettings, HttpConnectionValidator>(ConnectionType.Http, () => TechnicalAcronyms.Http);
            registry.Register<DatabaseConnection, DatabaseSettings, DatabaseConnectionValidator>(ConnectionType.Database, () => TechnicalTerms.Database);
            registry.Register<AzureIotHubConnection, AzureIotHubSettings, AzureIotConnectionValidator>(ConnectionType.AzureIotHub, () => TechnicalTerms.AzureIotHub);

            return registry;
        });
    };

    public override Func<IServiceProvider, Task> InitializeServices => async (services) =>
    {
        services.UseHelp();
        services.UseInstanceManagement();
        services.UseOnboarding();

        await services.GetRequiredService<ClientTimeProvider>().Initialize();

#if DEBUG
        try
        {
            var registry = services.GetRequiredService<INavTileRegistry<SharedClientModule>>();
            if (registry.Any(i => i.ComponentType == typeof(MqttViewerNavTile)))
                return;

            var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);
            registry.Add<MqttViewerNavTile>(Constants.MqttViewerNavTileId, linkTarget: Constants.MqttViewerRoute, group: NavTileGroup.Administration,
                authorizationRequirement: accessLevelAuthorizationRequirement);
        }
        catch (ObjectDisposedException)
        {
            // this happens...
        }
#endif
    };
}
