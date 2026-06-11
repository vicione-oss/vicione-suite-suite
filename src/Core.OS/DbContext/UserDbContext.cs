using Core.OS.UserManagement.Entities;
using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext;

public abstract class UserDbContext(DbContextOptions options) : IdentityDbContext<SuiteUser, SuiteRole, string>(options), IUserDbContext
{
    internal const string DbSchemaName = "user";

    public string DefaultSchemaName => DbSchemaName;
    public IEnumerable<Type> NotSynchronizedEntityTypes => [typeof(UserTicket)];

    public DbSet<UserTicket> Tickets => Set<UserTicket>();

    public Task MigrateAsync(CancellationToken cancellationToken = default)
        => Database.MigrateAsync(cancellationToken);

    protected sealed override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(DefaultSchemaName);
        base.OnModelCreating(builder);
    }
}

public sealed class UserDbContextSqlite(DbContextOptions<UserDbContextSqlite> options) : UserDbContext(options), ISqliteDbContext
{
}

public sealed class UserDbContextPostgres(DbContextOptions<UserDbContextPostgres> options) : UserDbContext(options), IPostgresDbContext
{
}

public sealed class InternalSecurityDbContextSqliteFactory : IDesignTimeDbContextFactory<UserDbContextSqlite>
{
    public UserDbContextSqlite CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddIdentity<SuiteUser, SuiteRole>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        }).AddEntityFrameworkStores<UserDbContextSqlite>();
        var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<UserDbContextSqlite>();
        optionsBuilder.UseApplicationServiceProvider(serviceProvider);
        optionsBuilder.UseSqlite(); 
        
        return new UserDbContextSqlite(optionsBuilder.Options);
    }
}

public sealed class InternalSecurityDbContextPostgresFactory : IDesignTimeDbContextFactory<UserDbContextPostgres>
{
    public UserDbContextPostgres CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddIdentity<SuiteUser, SuiteRole>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        });
        var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<UserDbContextPostgres>();
        optionsBuilder.UseApplicationServiceProvider(serviceProvider);
        optionsBuilder.UseNpgsql();

        return new UserDbContextPostgres(optionsBuilder.Options);
    }
}
