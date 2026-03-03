using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Services;

internal sealed partial class UserTicketCleanupService(IServiceProvider serviceProvider,
        IOptions<UserManagementOptions> options,
        ILogger<UserTicketCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = options.Value.UserTicket.CleanupInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("{Service} started with interval {Interval}", nameof(UserTicketCleanupService), _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IUserDbContext>();

                await DeleteExpiredTicketsWork(dbContext, stoppingToken);

                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // ignore gracefully
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception on deleting expired tickets.");
            }
        }

        logger.LogDebug("{Service} is stopping.", nameof(UserTicketCleanupService));
    }

    private async Task DeleteExpiredTicketsWork(IUserDbContext dbContext, CancellationToken cancellationToken)
    {
        logger.LogDebug("Deleting expired tickets");

        foreach (var ticket in dbContext.Tickets.AsEnumerable().Where(k => DateTimeOffset.UtcNow >= k.Expires))
        {
            dbContext.Tickets.Remove(ticket);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
