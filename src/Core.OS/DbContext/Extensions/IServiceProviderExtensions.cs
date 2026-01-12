using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext.Extensions;

internal static class IServiceProviderExtensions
{
    public static Task MigrateContext<TDbContext>(this IServiceProvider scopedServices,
        CancellationToken cancellationToken = default)
        where TDbContext : IModuleDbContext
    {
        var context = scopedServices.GetRequiredService<TDbContext>();
        return context.Instance.Database.MigrateAsync(cancellationToken);
    }
}
