using AwesomeAssertions;
using Core.OS.DbContext;
using Core.OS.UserManagement.Configuration;
using Core.OS.UserManagement.Entities;
using Core.OS.UserManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.OS.Tests.UserManagement.Services;

public class UserTicketCleanupServiceTests
{
    private readonly SqliteConnection _connection = TestExtensions.CreateSqliteMemoryConnection();

    private ServiceProvider SetupServiceProvider(out UserDbContext dbContext)
    {
        var userOptions = Options.Create(new UserManagementOptions
        {
            UserTicket = new()
        });

        dbContext = TestExtensions.CreateUserDbContextSqlite(_connection);

        var services = new ServiceCollection();
        services.AddSingleton<IUserDbContext>(dbContext);
        services.AddLogging();
        services.AddSingleton(userOptions);
        services.AddSingleton<UserTicketCleanupService>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_remove_only_expired_tickets_and_save_changes_each_iteration()
    {
        // Arrange
        var nowPast = DateTimeOffset.UtcNow.AddMinutes(-5);
        var nowFuture = DateTimeOffset.UtcNow.AddMinutes(5);

        var t1 = new UserTicket { Id = Guid.NewGuid(), UserId = "Tester", Value = [], Expires = nowPast }; // expired -> remove
        var t2 = new UserTicket { Id = Guid.NewGuid(), UserId = "Tester", Value = [], Expires = nowFuture }; // keep

        await using var services = SetupServiceProvider(out var dbContext);
        dbContext.Tickets.Add(t1);
        dbContext.Tickets.Add(t2);
        await dbContext.SaveChangesAsync();

        var sut = services.GetRequiredService<UserTicketCleanupService>();

        using var cts = new CancellationTokenSource();

        // Act
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(100);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }

        // Assert
        dbContext.Tickets.SingleOrDefault(k => k.Id == t1.Id).Should().BeNull();
        dbContext.Tickets.SingleOrDefault(k => k.Id == t2.Id).Should().NotBeNull();
    }
}
