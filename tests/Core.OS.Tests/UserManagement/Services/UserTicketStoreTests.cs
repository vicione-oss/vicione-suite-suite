using System.Security.Claims;
using AwesomeAssertions;
using Core.OS.DbContext;
using Core.OS.Persistence;
using Core.OS.UserManagement.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Core.OS.Tests.UserManagement.Services;

public class UserTicketStoreTests
{
    private readonly ILogger<UserTicketStore> _logger = Substitute.For<ILogger<UserTicketStore>>();
    private readonly SqliteConnection _connection = TestExtensions.CreateSqliteMemoryConnection();

    private ServiceProvider SetupServiceProvider(out UserDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_logger);

        dbContext = TestExtensions.CreateUserDbContextSqlite(_connection);

        services.AddSingleton<IUserDbContext>(dbContext);

        return services.BuildServiceProvider();
    }

    public sealed class RemoveAsync : UserTicketStoreTests
    {
        [Fact]
        public async Task Should_do_nothing_when_key_is_not_a_guid()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);

            var ticket = new UserTicket { Id = Guid.NewGuid(), UserId = "Tester", Value = [1, 2, 3] }; // expired -> remove
            dbContext.Tickets.Add(ticket);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            await store.RemoveAsync("not-a-guid");

            // Assert
            (await dbContext.Tickets.FindAsync([ticket.Id], TestContext.Current.CancellationToken)).Should().NotBeNull();
        }

        [Fact]
        public async Task Should_remove_existing_ticket_and_save_changes()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);

            var ticket = new UserTicket { Id = Guid.NewGuid(), UserId = "Tester", Value = [1, 2, 3] }; // expired -> remove
            dbContext.Tickets.Add(ticket);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            await store.RemoveAsync(ticket.Id.ToString());

            // Assert
            (await dbContext.Tickets.FindAsync([ticket.Id], TestContext.Current.CancellationToken)).Should().BeNull();
        }

        [Fact]
        public async Task Should_not_fail_when_ticket_is_not_found()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);

            // Act
            await store.RemoveAsync(Guid.NewGuid().ToString());

            // Assert
            (await dbContext.Tickets.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }
    }

    public sealed class RenewAsync : UserTicketStoreTests
    {
        [Fact]
        public async Task Should_do_nothing_when_key_is_not_a_guid()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);
            var authTicket = BuildAuthTicket("any", "user", DateTimeOffset.UtcNow.AddMinutes(10));

            // Act
            await store.RenewAsync("xxx", authTicket);

            // Assert
            (await dbContext.Tickets.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }

        [Fact]
        public async Task Should_update_ticket_value_and_timestamps_and_save()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);

            var id = Guid.NewGuid();
            var lastActivity = DateTimeOffset.UtcNow.AddHours(-1);
            var existing = new UserTicket
            {
                Id = id,
                UserId = "Tester",
                Value = [9, 9],
                LastActivity = lastActivity,
                Expires = DateTimeOffset.UtcNow.AddMinutes(-30)
            };
            dbContext.Tickets.Add(existing);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            var expires = DateTimeOffset.UtcNow.AddMinutes(20);
            var authTicket = BuildAuthTicket(Shared.Constants.AuthenticationSchema, userName: "Tester", expiresUtc: expires);

            // Act
            await store.RenewAsync(id.ToString(), authTicket);

            // Assert
            var reloaded = await dbContext.Tickets.FindAsync([id], TestContext.Current.CancellationToken);
            reloaded.Should().NotBeNull();
            reloaded.Value.Should().NotBeNullOrEmpty();
            reloaded.LastActivity.Should().BeAfter(lastActivity);
            reloaded.Expires.Should().BeCloseTo(expires, TimeSpan.FromSeconds(1));
        }
    }

    public sealed class RetrieveAsync : UserTicketStoreTests
    {
        [Fact]
        public async Task Should_return_null_when_key_is_not_a_guid()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out _);
            var store = new UserTicketStore(serviceProvider);

            // Act
            var result = await store.RetrieveAsync("nope");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Should_return_null_when_ticket_not_found()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out _);
            var store = new UserTicketStore(serviceProvider);

            // Act
            var result = await store.RetrieveAsync(Guid.NewGuid().ToString());

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Should_update_last_activity_and_return_deserialized_ticket()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);

            var id = Guid.NewGuid();
            var original = BuildAuthTicket(Shared.Constants.AuthenticationSchema, "userA", DateTimeOffset.UtcNow.AddMinutes(15));
            var value = TicketSerializer.Default.Serialize(original);

            var lastActivity = DateTimeOffset.UtcNow.AddHours(-1);
            var existing = new UserTicket
            {
                Id = id,
                UserId = "userA",
                Value = value,
                LastActivity = lastActivity,
                Expires = DateTimeOffset.UtcNow.AddMinutes(10)
            };
            dbContext.Tickets.Add(existing);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Act
            var result = await store.RetrieveAsync(id.ToString());

            // Assert
            result.Should().NotBeNull();
            result.AuthenticationScheme.Should().Be(original.AuthenticationScheme);
            result.Principal.Identity!.Name.Should().Be("userA");

            var reloaded = await dbContext.Tickets.FindAsync([id], TestContext.Current.CancellationToken);
            reloaded!.LastActivity!.Should().BeAfter(lastActivity);
        }
    }

    public sealed class StoreAsync : UserTicketStoreTests
    {
        [Fact]
        public async Task Should_return_empty_when_user_id_is_missing()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);

            var wrongSchemeTicket = BuildAuthTicket(authenticationScheme: "other", userName: null, expiresUtc: null);

            // Act
            var result = await store.StoreAsync(wrongSchemeTicket);

            // Assert
            result.Should().BeEmpty();
            (await dbContext.Tickets.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        }

        [Fact]
        public async Task Should_persist_ticket_and_return_new_id_when_valid_userid_available()
        {
            // Arrange
            await using var serviceProvider = SetupServiceProvider(out var dbContext);
            var store = new UserTicketStore(serviceProvider);

            var expires = DateTimeOffset.UtcNow.AddMinutes(30);
            var ticket = BuildAuthTicket(Shared.Constants.AuthenticationSchema, userName: "Tester", expiresUtc: expires);

            // Act
            var key = await store.StoreAsync(ticket);

            // Assert
            key.Should().NotBeNullOrWhiteSpace();
            Guid.TryParse(key, out var id).Should().BeTrue();

            var saved = await dbContext.Tickets.FindAsync([id], TestContext.Current.CancellationToken);
            saved.Should().NotBeNull();
            saved!.UserId.Should().Be("Tester");
            saved.Value.Should().NotBeNullOrEmpty();
            if (ticket.Properties.ExpiresUtc.HasValue)
                saved.Expires.Should().BeCloseTo(expires, TimeSpan.FromSeconds(1));
        }
    }

    private static AuthenticationTicket BuildAuthTicket(string authenticationScheme, string? userName = "user", DateTimeOffset? expiresUtc = null)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, userName ?? string.Empty),
        ], authenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        var props = new AuthenticationProperties();
        if (expiresUtc.HasValue)
            props.ExpiresUtc = expiresUtc;

        return new AuthenticationTicket(principal, props, authenticationScheme);
    }
}
