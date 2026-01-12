using System.Security.Claims;
using Core.OS.UserManagement.Configuration;
using Core.Shared.UserManagement.Comparers;
using Core.Shared.UserManagement.Configuration;
using Microsoft.AspNetCore.Identity;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;

namespace Core.OS.UserManagement.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddUserManagement(this IServiceCollection services)
    {
        services.AddSdkAuthorization();

        services.AddModuleFeature(_ => new ModuleFeature(Shared.Constants.SystemModuleId, Shared.Constants.SystemModuleId, "The default system administration permissions"));
        services.AddSingleton<IEqualityComparer<Claim>, ClaimEqualityComparer>();

        services.AddScoped<IAdministratorNameProvider, AdministratorNameProvider>();
        services.AddScoped<IAdministratorInitialPasswordProvider, AdministratorInitialPasswordProvider>();

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            // Force Identity's security stamp to be validated every 10 seconds.
            options.ValidationInterval = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
