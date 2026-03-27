using Microsoft.EntityFrameworkCore;
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

        await context.MigrateAsync(cancellationToken);

        logger.LogTrace("Migrating database finished");
    }
}
