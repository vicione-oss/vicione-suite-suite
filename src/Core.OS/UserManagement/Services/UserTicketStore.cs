using Core.OS.DbContext;
using Core.OS.UserManagement.Entities;
using Core.Shared.Authorization.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Core.OS.Persistence;

/// <summary>
/// Uses <see cref="IUserDbContext"/> to manage user session in <see cref="IUserDbContext.Tickets"/> table.
/// </summary>
internal sealed partial class UserTicketStore(IServiceProvider services) : ITicketStore
{
    private readonly ILogger<UserTicketStore> _logger = services.GetRequiredService<ILogger<UserTicketStore>>();

    /// <inheritdoc/>
    public async Task RemoveAsync(string key)
    {
        LogMethodWithKey(_logger, nameof(RemoveAsync), key);

        if (!Guid.TryParse(key, out var id))
            return;

        // try removing existing ticket on e.g. logout
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUserDbContext>();

        await dbContext.Tickets
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task RenewAsync(string key, AuthenticationTicket authTicket)
    {
        LogMethodWithKey(_logger, nameof(RenewAsync), key);

        if (!Guid.TryParse(key, out var id))
            return;

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUserDbContext>();

        var ticket = await dbContext.Tickets.FindAsync(id);
        if (ticket is null)
            return;

        // if we have found a ticket renew it's data
        ticket.Value = SerializeToBytes(authTicket);
        ticket.LastActivity = DateTimeOffset.UtcNow;
        ticket.Expires = authTicket.Properties.ExpiresUtc;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            LogFailedToUpdateLastActivity(_logger, key);
        }
        catch (Exception ex)
        {
            LogFailedToRenewTicket(_logger, ex, key);
        }
    }

    /// <inheritdoc/>
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        // RetrieveAsync is called multiple times per request
        LogMethodWithKey(_logger, nameof(RetrieveAsync), key);

        if (!Guid.TryParse(key, out var id))
            return null;

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUserDbContext>();

        // check if we have a ticket for the key (browser-session-id)
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
            return null;

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UserTicketStore>>();

        try
        {
            await dbContext.Tickets
                .Where(t => t.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastActivity, ticket.LastActivity));

            LogLastActivityUpdate(logger, ticket.Id);
        }
        catch (DbUpdateException)
        {
            LogFailedToUpdateLastActivity(logger, key);
        }
        catch (Exception ex)
        {
            LogFailedToRenewTicket(logger, ex, key);
        }

        return TicketSerializer.Default.Deserialize(ticket.Value);
    }

    /// <inheritdoc/>
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var userId = string.Empty;
        var nameIdentifier = ticket.Principal.GetUserName();

        if (ticket.AuthenticationScheme == Shared.Constants.AuthenticationSchema)
            userId = nameIdentifier;

        LogMethodWithKey(_logger, nameof(StoreAsync), userId ?? string.Empty);

        // just to ensure there's nothing bad
        if (string.IsNullOrWhiteSpace(userId))
        {
            StoreWithoutUserId(_logger);
            return string.Empty;
        }

        var authenticationTicket = new UserTicket()
        {
            UserId = userId,
            LastActivity = DateTimeOffset.UtcNow,
            Value = SerializeToBytes(ticket),
        };

        var expiresUtc = ticket.Properties.ExpiresUtc;
        if (expiresUtc.HasValue)
            authenticationTicket.Expires = expiresUtc.Value;

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IUserDbContext>();

        await dbContext.Tickets.AddAsync(authenticationTicket);
        await dbContext.SaveChangesAsync();

        return authenticationTicket.Id.ToString();
    }

    private static byte[] SerializeToBytes(AuthenticationTicket source)
        => TicketSerializer.Default.Serialize(source);

    [LoggerMessage(LogLevel.Warning, "Attempt to store auth ticket without valid user id")]
    private static partial void StoreWithoutUserId(ILogger<UserTicketStore> logger);

    [LoggerMessage(LogLevel.Trace, "{Method} key='{Key}'")]
    private static partial void LogMethodWithKey(ILogger<UserTicketStore> logger, string method, string key);

    [LoggerMessage(LogLevel.Trace, "Update last activity of ticket '{Ticket}'")]
    private static partial void LogLastActivityUpdate(ILogger<UserTicketStore> logger, Guid ticket);

    [LoggerMessage(LogLevel.Warning, "Failed to update last activity for key={Key}")]
    private static partial void LogFailedToUpdateLastActivity(ILogger<UserTicketStore> logger, string key);

    [LoggerMessage(LogLevel.Error, "Failed to refresh ticket for key={Key}")]
    private static partial void LogFailedToRenewTicket(ILogger<UserTicketStore> logger, Exception exception, string key);
}
