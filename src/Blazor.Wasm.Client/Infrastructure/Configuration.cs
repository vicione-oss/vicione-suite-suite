using Blazor.Shared.Services;
using Blazor.Wasm.Client.Infrastructure.HealthChecks;
using Blazor.Wasm.Client.Infrastructure.Logging;
using Blazor.Wasm.Client.Infrastructure.Mediator;
using Blazor.Wasm.Client.Infrastructure.Modules;
using Blazor.Wasm.Client.Infrastructure.SignalR;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sdk.Client.Infrastructure;

namespace Blazor.Wasm.Client.Infrastructure;

internal static class Configuration
{
    private const string BackendClientName = "Core.OS.API";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string baseAddress)
    {
        // Logging
        services
            .AddTransient<LogHeaderProcessingHandler>()
            .AddTransient<IBackendLogService, BackendLogHttpService>();

        // Http
        services
            .AddHttpClient(BackendClientName, client => client.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<LogHeaderProcessingHandler>();

        // todo: remove direct HttpClient usage 
        services
            .AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(BackendClientName));

        // Common
        services.AddScoped<InstanceHealthService>();

        //Messaging:
        services
            .AddScoped<IUiMediator, UiMediator>()
            .AddTransient<IBackendModuleHttpClient, BackendModuleHttpClient>();

        //SignalR
        services
            .AddSingleton<ClientMessageHub>()
            .AddSingleton<IClientMessageHub, ClientMessageHub>(s => s.GetRequiredService<ClientMessageHub>())
            .AddSingleton<IMessageHubClient, ClientMessageHub>(s => s.GetRequiredService<ClientMessageHub>());

        return services;
    }


    public static HttpClient CreateBackendClient(this IHttpClientFactory factory) => factory.CreateClient(BackendClientName);


    public static IServiceCollection AddCompatibleHostEnvironment(this IServiceCollection services)
    {
        services.AddTransient<IHostEnvironment>(s =>
        {
            var env = s.GetRequiredService<IWebAssemblyHostEnvironment>();
            return new CompatibleHostEnvironment(env.Environment);
        });

        return services;
    }

    /// <summary>
    /// wasm usually adds IWebAssemblyHostEnvironment but blazor server components might inject it
    /// so we provide at least something
    /// </summary>
    private class CompatibleHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
