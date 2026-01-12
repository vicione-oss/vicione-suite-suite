using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext.Extensions;

internal static class IServiceProviderExtensions
{
    public static async Task MigrateContext<TDbContext>(this IServiceProvider scopedServices,
        CancellationToken cancellationToken = default)
        where TDbContext : IModuleDbContext
    {
        var context = scopedServices.GetRequiredService<TDbContext>();
        var logger = scopedServices.GetRequiredService<ILogger<TDbContext>>();

        logger.LogTrace("Migration of database started");

        // If we have a lock entry in `__EFMigrationsLock` table already we get back a task
        // that is waiting for activation...forever! Therefore we clear the lock because
        // suite is lord of migrations and there will be only 1 suite accessing the db
        await TryToClearMigrationsLock(context.Instance.Database, cancellationToken);

        await context.Instance.Database.MigrateAsync(cancellationToken);

        logger.LogTrace("Migrating database finished");
    }

    private static async Task TryToClearMigrationsLock(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        try
        {
            await database.ExecuteSqlRawAsync("DELETE FROM __EFMigrationsLock", cancellationToken);
        }
        catch (SqliteException)
        {
            // no migrations run yet - table does not exist
        }
        catch (NpgsqlException)
        {
            // no migrations run yet - table does not exist
        }
    }
}
