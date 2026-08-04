using System.Reflection;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IMvcBuilder AddTestSetupMvc(Assembly[] assemblies)
        {
            var mvcBuilder = services.AddMvc();

            services
                .AddTestPersistence()
                .AddTestSecurity();

            return mvcBuilder;
        }
        
        private IServiceCollection AddTestPersistence()
        {
            // Add ApplicationDbContext using an in-memory database for testing.
            services.AddDbContext<TestDbContext>(opts =>
            {
                opts.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
            }, ServiceLifetime.Singleton, ServiceLifetime.Singleton);

            return services;
        }

        public IServiceCollection AddTestIdentity()
        {
            services.AddDefaultIdentity<IdentityUser>()
                .AddRoles<SuiteRole>()
                .AddEntityFrameworkStores<TestDbContext>();

            return services;
        }

        public IServiceCollection SetupUserManager(IQueryableUserStore<SuiteUser> userStore)
        {
            services.AddSingleton(userStore);
            services.AddSingleton(Substitute.For<IOptions<IdentityOptions>>());
            services.AddSingleton(Substitute.For<IPasswordHasher<SuiteUser>>());
            services.AddSingleton(Substitute.For<IUserValidator<SuiteUser>>());
            services.AddSingleton(Substitute.For<IPasswordValidator<SuiteUser>>());
            services.AddSingleton(Substitute.For<ILookupNormalizer>());
            services.AddSingleton(Substitute.For<ILogger<UserManager<SuiteUser>>>());
            services.AddSingleton<UserManager<SuiteUser>>(svc
                => new UserManager<SuiteUser>(
                    userStore,
                    svc.GetRequiredService<IOptions<IdentityOptions>>(),
                    svc.GetRequiredService<IPasswordHasher<SuiteUser>>(),
                    svc.GetServices<IUserValidator<SuiteUser>>(),
                    svc.GetServices<IPasswordValidator<SuiteUser>>(),
                    svc.GetRequiredService<ILookupNormalizer>(),
                    new IdentityErrorDescriber(),
                    svc,
                    svc.GetRequiredService<ILogger<UserManager<SuiteUser>>>()
                )
            );

            return services;
        }
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options) : IdentityDbContext<IdentityUser>(options);
}
