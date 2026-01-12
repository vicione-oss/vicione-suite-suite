using System.Reflection;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Testing.Backend;

namespace Core.OS.Tests;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddTestSetupMvc(this IServiceCollection services, Assembly[] assemblies)
    {
        var mvcBuilder = services.AddMvc();

        services
            .AddTestPersistence()
            .AddTestSecurity();

        return mvcBuilder;
    }

    public static IApplicationBuilder ConfigureTestSetup(this IApplicationBuilder app)
    {
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapControllers();
            endpoints.MapFallbackToFile("index.html");
        });

        return app;
    }

    public static IServiceCollection AddTestPersistence(this IServiceCollection services)
    {
        // Add ApplicationDbContext using an in-memory database for testing.
        services.AddDbContext<TestDbContext>(opts =>
        {
            opts.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
        }, ServiceLifetime.Singleton, ServiceLifetime.Singleton);

        return services;
    }

    public static IServiceCollection AddTestIdentity(this IServiceCollection services)
    {
        services.AddDefaultIdentity<IdentityUser>()
            .AddRoles<SuiteRole>()
            .AddEntityFrameworkStores<TestDbContext>();

        return services;
    }

    public static IServiceCollection SetupUserManager(this IServiceCollection services, IQueryableUserStore<SuiteUser> userStore)
    {
        services.AddSingleton(userStore);
        services.AddSingleton(Substitute.For<IOptions<IdentityOptions>>());
        services.AddSingleton(Substitute.For<IPasswordHasher<SuiteUser>>());
        services.AddSingleton(Substitute.For<IUserValidator<SuiteUser>>());
        services.AddSingleton(Substitute.For<IPasswordValidator<SuiteUser>>());
        services.AddSingleton(Substitute.For<ILookupNormalizer>());
        services.AddSingleton(Substitute.For<ILogger<UserManager<SuiteUser>>>());
        services.AddSingleton<UserManager<SuiteUser>>(svc =>
        {
            return new UserManager<SuiteUser>(
                userStore,
                svc.GetRequiredService<IOptions<IdentityOptions>>(),
                svc.GetRequiredService<IPasswordHasher<SuiteUser>>(),
                svc.GetServices<IUserValidator<SuiteUser>>(),
                svc.GetServices<IPasswordValidator<SuiteUser>>(),
                svc.GetRequiredService<ILookupNormalizer>(),
                new IdentityErrorDescriber(),
                svc,
                svc.GetRequiredService<ILogger<UserManager<SuiteUser>>>()
                );
        });

        return services;
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options) : IdentityDbContext<IdentityUser>(options);
}
