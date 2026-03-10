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
                // ignore gracefully
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
        
        LogDeletingExpiredTickets(logger);

        foreach (var ticket in dbContext.Tickets.AsEnumerable().Where(k => DateTimeOffset.UtcNow >= k.Expires))
        {
            dbContext.Tickets.Remove(ticket);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(LogLevel.Debug, "{Service} started with interval {Interval}")]
    static partial void LogServiceStartedWithIntervalInterval(ILogger<UserTicketCleanupService> logger, string Service, TimeSpan Interval);

    [LoggerMessage(LogLevel.Error, "Unhandled exception on deleting expired tickets.")]
    static partial void LogUnhandledExceptionOnDeletingExpiredTickets(ILogger<UserTicketCleanupService> logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "{Service} is stopping.")]
    static partial void LogServiceIsStopping(ILogger<UserTicketCleanupService> logger, string Service);

    [LoggerMessage(LogLevel.Debug, "Deleting expired tickets")]
    static partial void LogDeletingExpiredTickets(ILogger<UserTicketCleanupService> logger);
}
