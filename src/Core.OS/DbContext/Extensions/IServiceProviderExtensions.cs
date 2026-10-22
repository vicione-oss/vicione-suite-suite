using Sdk.Backend.Persistence;

namespace Core.OS.DbContext.Extensions;

internal static partial class IServiceProviderExtensions
{
    public static async Task MigrateContext<TDbContext>(this IServiceProvider scopedServices,
        CancellationToken cancellationToken = default)
        where TDbContext : IModuleDbContext
    {
        var context = scopedServices.GetRequiredService<TDbContext>();
        var logger = scopedServices.GetRequiredService<ILogger<TDbContext>>();

        LogMigrationStarted(logger);

        await context.MigrateAsync(cancellationToken);

        LogMigrationFinished(logger);
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "Migration of database started")]
    private static partial void LogMigrationStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Migrating database finished")]
    private static partial void LogMigrationFinished(ILogger logger);
}
