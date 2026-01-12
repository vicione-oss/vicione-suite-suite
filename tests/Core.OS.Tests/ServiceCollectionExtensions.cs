using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TestDbContext>();

        return services;
    }
    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options) : IdentityDbContext<IdentityUser>(options);
}
