using Core.OS.DbContext;
using Core.Shared.UserManagement.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Core.OS.Tests.Persistence;

public abstract class RemoveExpiryDateOfUsersWithoutPasswordMigrationTests
{
    private const string PreviousMigration = "AddPasskeySupport";
    private static readonly DateTimeOffset ExpiryDateSetByAdmin = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Should_remove_the_expiry_date_of_a_user_without_password()
    {
        // Arrange
        var user = new SuiteUser { UserName = "oidc.user", PasswordExpirationDate = DateTimeOffset.MinValue };

        // Act
        var expiryDate = await ExpiryDateAfterMigration(user);

        // Assert
        expiryDate.Should().BeNull();
    }

    [Fact]
    public async Task Should_keep_the_expiry_date_of_a_user_with_password()
    {
        // Arrange
        var user = new SuiteUser
        {
            UserName = "admin",
            PasswordHash = "hash",
            PasswordExpirationDate = DateTimeOffset.MinValue
        };

        // Act
        var expiryDate = await ExpiryDateAfterMigration(user);

        // Assert
        expiryDate.Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task Should_keep_an_expiry_date_set_by_an_admin_for_a_user_without_password()
    {
        // Arrange
        var user = new SuiteUser { UserName = "oidc.user", PasswordExpirationDate = ExpiryDateSetByAdmin };

        // Act
        var expiryDate = await ExpiryDateAfterMigration(user);

        // Assert
        expiryDate.Should().Be(ExpiryDateSetByAdmin);
    }

    protected abstract UserDbContext CreateEmptyDbContext();

    private async Task<DateTimeOffset?> ExpiryDateAfterMigration(SuiteUser user)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = CreateEmptyDbContext();
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration, cancellationToken);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await migrator.MigrateAsync(cancellationToken: cancellationToken);

        await dbContext.Entry(user).ReloadAsync(cancellationToken);

        return user.PasswordExpirationDate;
    }

    public sealed class Sqlite : RemoveExpiryDateOfUsersWithoutPasswordMigrationTests
    {
        protected override UserDbContext CreateEmptyDbContext()
        {
            var dbContext = new InternalSecurityDbContextSqliteFactory().CreateDbContext([]);
            dbContext.Database.SetConnectionString("Data Source=:memory:");
            dbContext.Database.OpenConnection();

            return dbContext;
        }
    }

    /// <summary>
    /// Needs a real PostgreSQL, because Npgsql stores <see cref="DateTimeOffset.MinValue" /> as <c>-infinity</c>.
    /// CI provides one as a service container; see docs/integration-testing.md.
    /// </summary>
    [Trait(Traits.Category, Traits.Integration)]
    public sealed class Postgres : RemoveExpiryDateOfUsersWithoutPasswordMigrationTests
    {
        protected override UserDbContext CreateEmptyDbContext()
        {
            var dbContext = new InternalSecurityDbContextPostgresFactory().CreateDbContext([]);
            dbContext.Database.SetConnectionString(PostgresTestConnection.ForDatabase("userdbmigration"));
            dbContext.Database.EnsureDeleted();

            return dbContext;
        }
    }
}
