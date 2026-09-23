using Core.Module.Contracts;
using Core.OS.DbContext;
using Core.OS.Modules;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Authorization.Extensions;
using Sdk.Backend.Modules;
using Sdk.Modules;
using Sdk.Testing.Backend;
using TestModule.Backend;

namespace Core.OS.Tests;

internal static class TestManagerUtils
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddModuleManagerWithTestModule()
        {
            var moduleManager = Substitute.For<IModuleHost>();
            var modules = GetModuleBundles();

            moduleManager.GetModule<TestBackendModule>().Returns(modules.First().Module);
            moduleManager.GetModules().Returns(modules.Select(k => k.Module));
            moduleManager.GetModuleAssemblies().Returns(modules.Select(k => k.Assembly));
            // Normally done by ModuleHost.AddModuleServices.
            var name = ModuleIdResolver.GetModuleName(TestBackendModule.Id);
            services.AddModuleFeature(_ => new ModuleFeature(TestBackendModule.Id, name, $"The default permission for {name}."));

            services.AddSingleton(moduleManager);

            return services;
        }

        public IServiceCollection AddApplicationDbContextsInMemory(bool autoMigrate = true)
        {
            services.AddSingleton<ApplicationDbContext>(_ => TestDbContextFactory.CreateSqliteContext<ApplicationDbContextSqlite>(autoMigrate));
            services.AddTransient<IApplicationDbContext>(s => s.GetRequiredService<ApplicationDbContext>());

            return services;
        }

        public IServiceCollection AddUserDbContextsInMemory(bool autoMigrate = true)
        {
            services.AddIdentityCore<SuiteUser>(options =>
                {
                    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
                })
                .AddRoles<SuiteRole>()
                .AddEntityFrameworkStores<UserDbContext>();

            services.AddSingleton<UserDbContext>(serviceProvider =>
            {
                var connection = new SqliteConnection($"DataSource={TestDbContextFactory.DataSourceInMemory}");
                connection.Open();

                return TestDbContextFactory.CreateSqliteContext<UserDbContextSqlite>(connection,
                    autoMigrate, optionsBuilder =>
                    {
                        optionsBuilder.UseApplicationServiceProvider(serviceProvider);

                        // TODO(#2695): ignoring these warnings is currently the only way to keep ApplicationWorkerTest
                        // from failing sporadically.
                        optionsBuilder.ConfigureWarnings(warnings
                            => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
                    });
            });
            services.AddTransient<IUserDbContext>(s => s.GetRequiredService<UserDbContext>());

            return services;
        }

        public IServiceCollection AddConnectionDbContextsInMemory(bool autoMigrate = true)
        {
            services.AddSingleton<ConnectionDbContext>(s => TestDbContextFactory.CreateSqliteContext<ConnectionDbContextSqlite>(autoMigrate));
            services.AddTransient<IConnectionDbContext>(s => s.GetRequiredService<ConnectionDbContext>());

            return services;
        }
    }

    public static async Task EnsureApplicationContextIsCreated(this IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    private static List<ModuleBundle<BackendModule>> GetModuleBundles()
        => [new ModuleBundle<BackendModule>(new TestBackendModule(), typeof(TestBackendModule).Assembly, typeof(TestBackendModule).Assembly.Location)];
}
