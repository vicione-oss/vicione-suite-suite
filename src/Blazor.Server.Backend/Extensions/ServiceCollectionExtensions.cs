using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Server.Backend.Extensions;

internal static class ServiceCollectionExtensions
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
}
