using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization.Extensions;

namespace Blazor.Shared.Authorization.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorSharedAuthorization(this IServiceCollection services)
    {
        services.AddSdkAuthorization();

        services.AddSingleton<IAuthorizationPolicyProvider, AccessLevelPolicyProvider>();

        return services;
    }
}
