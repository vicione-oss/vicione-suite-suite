using Core.Shared.UserManagement.Contracts;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext;

public abstract class UserDbContext(DbContextOptions options) : IdentityDbContext<SuiteUser>(options), IUserDbContext
{
    internal const string DbSchemaName = "user";

    public Microsoft.EntityFrameworkCore.DbContext Instance => this;
    public string DefaultSchemaName => DbSchemaName;
    public IEnumerable<Type> NotSynchronizedEntityTypes => [];

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
        var optionsBuilder = new DbContextOptionsBuilder<UserDbContextSqlite>();
        optionsBuilder.UseSqlite();

        return new UserDbContextSqlite(optionsBuilder.Options);
    }
}

public sealed class InternalSecurityDbContextPostgresFactory : IDesignTimeDbContextFactory<UserDbContextPostgres>
{
    public UserDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UserDbContextPostgres>();
        optionsBuilder.UseNpgsql();

        return new UserDbContextPostgres(optionsBuilder.Options);
    }
}
