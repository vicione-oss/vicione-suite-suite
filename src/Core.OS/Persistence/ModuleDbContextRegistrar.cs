using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Sdk.Backend.Modules;
using Sdk.Backend.Persistence;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Core.OS.Persistence;

public sealed class ModuleDbContextRegistrar : IModuleDbContextRegistrar
{
    public void Register<TDbContextInterface, TSqliteImplementation, TPostgresImplementation>(
        IServiceCollection services,
        string moduleId,
        Type moduleType,
        string sqliteDbName,
        bool enableSynchronization)
        where TDbContextInterface : IModuleDbContext
        where TSqliteImplementation : EfDbContext, ISqliteDbContext, TDbContextInterface
        where TPostgresImplementation : EfDbContext, IPostgresDbContext, TDbContextInterface
        => services.RegisterModuleDbContext<TDbContextInterface, TSqliteImplementation, TPostgresImplementation>(
            moduleId, moduleType, sqliteDbName, enableSynchronization);
}

internal static class ModuleDbContextServiceCollectionExtensions
{
    internal static IServiceCollection RegisterModuleDbContext<TDbContextInterface, TSqliteImplementation, TPostgresImplementation>(
        this IServiceCollection services,
        string moduleId,
        Type moduleType,
        string sqliteDbName,
        bool enableSynchronization)
        where TDbContextInterface : IModuleDbContext
        where TSqliteImplementation : EfDbContext, ISqliteDbContext, TDbContextInterface
        where TPostgresImplementation : EfDbContext, IPostgresDbContext, TDbContextInterface
    {
        services.AddSingleton(new ModuleContextTypeInformation(moduleId,
            typeof(TDbContextInterface),
            typeof(TDbContextInterface).FullName!));

        services.AddScoped(typeof(TDbContextInterface),
            s => Resolve<TSqliteImplementation, TPostgresImplementation>(s, moduleType, sqliteDbName, enableSynchronization));

        services.AddScoped(typeof(TPostgresImplementation).BaseType!,
            s => // allows to inject shared type - necessary for Sagas
                Resolve<TSqliteImplementation, TPostgresImplementation>(s, moduleType, sqliteDbName, enableSynchronization));

        return services;
    }

    private static IModuleDbContext Resolve<TSqliteImplementation, TPostgresImplementation>(
        IServiceProvider services,
        Type moduleType,
        string sqliteDbName,
        bool enableSynchronization)
        where TSqliteImplementation : EfDbContext, ISqliteDbContext
        where TPostgresImplementation : EfDbContext, IPostgresDbContext
    {
        if (services.GetService(typeof(IMasterDbConnectionStringProvider)) is not IMasterDbConnectionStringProvider connectionStringProvider)
        {
            return ActivatorUtilities.CreateInstance<TSqliteImplementation>(services,
                new DbContextOptionsBuilder<TSqliteImplementation>()
                    .UseSqlite(GetSqliteConnectionString(services, moduleType, sqliteDbName))
                    .ReplaceService<IMigrationsDatabaseLock, NoOpMigrationsDatabaseLock>()
                    .UseApplicationServiceProvider(services)
                    .Options);
        }

        var optionsBuilder = new DbContextOptionsBuilder<TPostgresImplementation>()
            .UseNpgsql(connectionStringProvider.ConnectionString)
            .ReplaceService<IMigrationsDatabaseLock, NoOpMigrationsDatabaseLock>()
            .UseApplicationServiceProvider(services);

        if (enableSynchronization)
            optionsBuilder.AddInterceptors(services.GetRequiredService<ISaveChangesInterceptor>());

        return ActivatorUtilities.CreateInstance<TPostgresImplementation>(services, optionsBuilder.Options);
    }

    private static string GetSqliteConnectionString(IServiceProvider services, Type moduleType, string sqliteDbName)
    {
        var serviceType = typeof(IWorkspaceProvider<>).MakeGenericType(moduleType);
        var wsService = services.GetRequiredService(serviceType);
        var workspace = (string)serviceType
            .GetProperty(nameof(IWorkspaceProvider<>.Home))!
            .GetValue(wsService)!;

        if (!sqliteDbName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            sqliteDbName += ".db";

        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(workspace, sqliteDbName)
        }.ConnectionString;
    }
}

