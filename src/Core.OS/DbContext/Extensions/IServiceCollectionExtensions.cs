using Core.OS.Modules;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddCoreDbContext<TBaseInterface, TSqliteImplementation, TPostgresImplementation>(
            this IServiceCollection services,
            string? sqliteDbName = null,
            bool enableSynchronization = true)
        where TBaseInterface : IModuleDbContext
        where TSqliteImplementation : Microsoft.EntityFrameworkCore.DbContext, ISqliteDbContext, TBaseInterface
        where TPostgresImplementation : Microsoft.EntityFrameworkCore.DbContext, IPostgresDbContext, TBaseInterface
    {
        if (!typeof(TBaseInterface).IsInterface)
            throw new InvalidOperationException($"{nameof(TBaseInterface)} must be an interface type!");

        services.AddSingleton(
            new DbContextResolverOptions<TBaseInterface>(typeof(SystemBackendModule),
                sqliteDbName ?? Shared.Constants.SystemModuleId,
                enableSynchronization));

        services.AddTransient<DbContextResolver<TSqliteImplementation, TPostgresImplementation, TBaseInterface>>();
        services.AddSingleton(new ModuleContextTypeInformation(Shared.Constants.SystemModuleId,
            typeof(TBaseInterface),
            typeof(TBaseInterface).FullName!));

        services.AddScoped(typeof(TBaseInterface),
            s => s.GetRequiredService<DbContextResolver<TSqliteImplementation, TPostgresImplementation, TBaseInterface>>()
                    .Resolve(s));

        services.AddScoped(typeof(TPostgresImplementation).BaseType!,
            s => //allows to inject shared type - necessary for Sagas
                s.GetRequiredService<DbContextResolver<TSqliteImplementation, TPostgresImplementation, TBaseInterface>>()
                    .Resolve(s));
        return services;
    }
}
