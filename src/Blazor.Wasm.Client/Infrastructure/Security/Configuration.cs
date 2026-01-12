using Blazor.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Blazor.Wasm.Client.Infrastructure.Security;

public static class Configuration
{
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services
            .AddAuthorizationCore()
            .AddSingleton<IAuthorizationHandler, ModuleAccessLevelHandler>()
            .AddScoped<CustomAuthStateProvider>()
            .AddScoped<AuthenticationStateProvider>(s => s.GetRequiredService<CustomAuthStateProvider>())
            .AddScoped<CookieHandler>()
            .AddScoped<IAuthApi, AuthApi>();

        return services;
    }

}
