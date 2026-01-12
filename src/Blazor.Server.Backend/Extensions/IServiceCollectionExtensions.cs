using Blazor.Server.Backend.Services;
using Blazor.Shared;
using Blazor.Shared.Connections.Components;
using Blazor.Shared.Connections.Validators;
using Blazor.Shared.Extensions;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Client.Connections;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddServerHttpClient(this IServiceCollection services)
    {
        // Server Side Blazor doesn't register HttpClient by default
        // Thanks to Robin Sue - Suchiman https://github.com/Suchiman/BlazorDualMode

        // Setup HttpClient for server side in a client side compatible fashion
        services.TryAddScoped(s =>
        {
            // Creating the URI helper needs to wait until the JS Runtime is initialized, so defer it.
            var uriHelper = s.GetRequiredService<NavigationManager>();
            return new HttpClient
            {
                BaseAddress = new Uri(uriHelper.BaseUri)
            };
        });

        return services;
    }

    public static IServiceCollection AddUiHostClientServices(this IServiceCollection services)
    {
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

        services.AddModuleFeature(_ => new ModuleFeature(Core.Shared.Constants.SystemModuleId, Constants.LogViewFeature, "Controls access to the 'Log viewer'-feature"));

        services.AddSingleton<IConnectionTypeUiRegistry, ConnectionTypeUiRegistry>(s =>
        {
            ConnectionTypeUiRegistry registry = new();

            registry.Register<MqttConnection, MqttSettings, MqttItemValidator>(ConnectionType.Mqtt, () => TechnicalAcronyms.Mqtt);
            registry.Register<HttpConnection, HttpSettings, HttpConnectionValidator>(ConnectionType.Http, () => TechnicalAcronyms.Http);
            registry.Register<SQLiteConnection, SQLiteConnectionSettings, SQLiteConnectionValidator>(ConnectionType.SQLite, () => nameof(ConnectionType.SQLite));
            registry.Register<PostgresConnection, PostgresConnectionSettings, PostgresConnectionValidator>(ConnectionType.Postgres, () => nameof(ConnectionType.Postgres));

            return registry;
        });

        return services;
    }
}
