using Blazor.Server.Backend.Services;
using Blazor.Shared.Connections.Components;
using Blazor.Shared.Connections.Validators;
using Blazor.Shared.Extensions;
using Blazor.Shared.Module.Services;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Client.Connections;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddLocalHttpClient()
        {
            // https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
            services.AddHttpClient<ILocalHttpClient, LocalHttpClient>((provider, client) =>
            {
                // AddressFeature is empty until kestrel is running, so the address is read lazily.
                var server = provider.GetRequiredService<IServer>();
                var addressFeature = server.Features.Get<IServerAddressesFeature>();
                var baseAddress = addressFeature?.Addresses.FirstOrDefault(k => k.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                    ?? addressFeature?.Addresses.FirstOrDefault(k => k.StartsWith("http", StringComparison.OrdinalIgnoreCase));
                if (baseAddress is null)
                {
                    throw new InvalidOperationException("Kestrel server is not yet running or no addresses are configured.");
                }

                client.BaseAddress = new Uri(baseAddress);
            });

            return services;
        }

        public IServiceCollection AddUiHostClientServices()
        {
            services.AddScoped<ClientTimeProvider>();
            services.AddScoped<IClientTimeProvider>(s => s.GetRequiredService<ClientTimeProvider>());
            services.AddKeyedScoped<TimeProvider, ClientTimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, (sp, _) => sp.GetRequiredService<ClientTimeProvider>());
            services.AddBlazorShared();

            // Server-specific implementations of the shared UI abstractions.
            services
                .AddSingleton<IClientModuleService, BlazorServerModuleService>()
                .AddScoped<IUiMediator, BlazorServerUiMediator>()
                .AddTransient<IBackendLogService, BackendLogService>()
                .AddTransient<INavigationService, NavigationService>();

            services.AddModuleFeature(_ => new ModuleFeature(Core.Shared.Constants.SystemModuleId, Shared.Constants.LogViewFeature, "Controls access to the 'Log viewer'-feature"));

            services.AddSingleton<IConnectionTypeUiRegistry, ConnectionTypeUiRegistry>(_ =>
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
}
