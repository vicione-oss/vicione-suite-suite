using Core.OS.Modules;
using Core.OS.Persistence;
using Constants = Core.Shared.Constants;

namespace Core.OS.DbContext.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the system DbContexts owned by Core.OS. Connection-specific contexts
        /// are registered alongside their own concern (see <c>AddConnectionServices</c>).
        /// </summary>
        internal IServiceCollection AddCoreDbContexts()
        {
            services.RegisterModuleDbContext<IApplicationDbContext, ApplicationDbContextSqlite, ApplicationDbContextPostgres>(
                Constants.SystemModuleId,
                typeof(SystemBackendModule),
                ApplicationDbContext.DbSchemaName,
                enableSynchronization: true);

            services.RegisterModuleDbContext<IUserDbContext, UserDbContextSqlite, UserDbContextPostgres>(
                Constants.SystemModuleId,
                typeof(SystemBackendModule),
                UserDbContext.DbSchemaName,
                enableSynchronization: true);

            return services;
        }
    }
}
