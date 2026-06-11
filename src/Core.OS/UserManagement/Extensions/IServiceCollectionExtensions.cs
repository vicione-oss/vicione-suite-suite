using System.Security.Claims;
using Core.OS.DbContext;
using Core.OS.Extensions;
using Core.OS.Modules;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Security;
using Core.Shared.Security;
using Core.Shared.UserManagement.Comparers;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Constants = Core.Shared.Constants;

namespace Core.OS.UserManagement.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddUserManagement(this IServiceCollection services)
    {
        services.AddSdkAuthorization();

        services.AddModuleFeature(_ => new ModuleFeature(Constants.SystemModuleId, Constants.SystemModuleId, "The default system administration permissions"));
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

    public static IServiceCollection AddIdentityAndExternalAuth(this IServiceCollection services,
        ConfigurationManager config,
        IModuleHost moduleHost)
    {
        var userManagementOptions = config.GetUserManagementOptions();
        var smtpOptions = config.GetSmtpOptions();
        // ui host might not know identity or suite user
        moduleHost.AddUiHostServices(services,
            (svc) =>
            {
                var identityBuilder = svc
                    .AddIdentity<SuiteUser, SuiteRole>(options =>
                    {
                        options.SignIn.RequireConfirmedAccount
                            = userManagementOptions.RequireAccountVerificationToLogIn && smtpOptions is not null;
                        options.Password.RequiredLength = Constants.MinimumPasswordLength;
                        options.Password.RequireDigit = true;
                        options.Password.RequireLowercase = true;
                        options.Password.RequireUppercase = true;
                        options.Password.RequireNonAlphanumeric = true;

                        options.User.RequireUniqueEmail = true;

                        options.Lockout.MaxFailedAccessAttempts = LockoutDefaults.MaxFailedAccessAttempts;
                        options.Lockout.DefaultLockoutTimeSpan = LockoutDefaults.LockoutDuration;
                        options.Lockout.AllowedForNewUsers = true;

                        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                    })
                    .AddEntityFrameworkStores<UserDbContext>()
                    .AddDefaultTokenProviders();

                AddExternalAuthentication(services, config);
                return identityBuilder;
            });

        return services;
    }

    private static void AddExternalAuthentication(IServiceCollection services, ConfigurationManager config)
    {
        services.AddTransient<IExternalAuthenticationSettings, ExternalAuthenticationSettings>();
        services.AddSingleton<IAuthenticationSchemeProvider, DynamicAuthenticationSchemeProvider>();
        services.ConfigureOptions<DynamicExternalIdProviderOptions>();
        services.AddAuthentication()
            .AddOpenIdConnect(DynamicExternalIdProviderOptions.OptionsName, _ =>
            {
                // will be handled by DynamicExternalIdProviderOptions
            });
    }
}
