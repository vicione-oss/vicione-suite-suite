using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Core.OS.UserManagement.Services;

internal sealed partial class UserTicketCleanupService(IServiceProvider serviceProvider,
        IOptions<UserManagementOptions> options,
        ILogger<UserTicketCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = options.Value.UserTicket.CleanupInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStartedWithIntervalInterval(logger, nameof(UserTicketCleanupService), _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredTicketsWork(stoppingToken);

                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation during shutdown needs no handling.
            }
            catch (Exception ex)
            {
                LogUnhandledExceptionOnDeletingExpiredTickets(logger, ex);
            }
        }

        LogServiceIsStopping(logger, nameof(UserTicketCleanupService));
    }

    internal async Task DeleteExpiredTicketsWork(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUserDbContext>();

        // This is a SQLite-specific limitation; on a real SQL Server or PostgreSQL deployment
        // a single-query ExecuteDeleteAsync with a DateTimeOffset predicate would translate fine.
        var now = DateTimeOffset.UtcNow;
        var expiredIds = dbContext.Tickets
            .AsNoTracking()
            .AsEnumerable()
            .Where(k => k.Expires <= now)
            .Select(k => k.Id)
            .ToList();

        if (expiredIds.Count == 0)
        {
            LogDeletedExpiredTickets(logger, 0);
            return;
        }

        var deleted = await dbContext.Tickets
            .Where(k => expiredIds.Contains(k.Id))
            .ExecuteDeleteAsync(cancellationToken);

        LogDeletedExpiredTickets(logger, deleted);
    }

    [LoggerMessage(LogLevel.Debug, "{Service} started with interval {Interval}")]
    static partial void LogServiceStartedWithIntervalInterval(ILogger<UserTicketCleanupService> logger, string Service, TimeSpan Interval);

    [LoggerMessage(LogLevel.Error, "Unhandled exception on deleting expired tickets.")]
    static partial void LogUnhandledExceptionOnDeletingExpiredTickets(ILogger<UserTicketCleanupService> logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "{Service} is stopping.")]
    static partial void LogServiceIsStopping(ILogger<UserTicketCleanupService> logger, string Service);

    [LoggerMessage(LogLevel.Debug, "Deleted {Count} expired tickets")]
    static partial void LogDeletedExpiredTickets(ILogger<UserTicketCleanupService> logger, int count);
}
