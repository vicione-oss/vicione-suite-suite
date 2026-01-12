using Core.Shared.Connections.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Extensions;
using Sdk.Backend.Persistence;
using Sdk.Connections.Contracts;

namespace Core.OS.DbContext;

/// <remarks>
/// To add migrations see README.md section Database migration
/// </remarks>
public class ConnectionDbContext : ModuleDbContext, IConnectionDbContext
{
    internal const string DbSchemaName = "connection";

    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<Tag> Tags => Set<Tag>();
    public override string DefaultSchemaName => DbSchemaName;

    public override IEnumerable<Type> NotSynchronizedEntityTypes => [typeof(ConnectionTag)];

    internal ConnectionDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
    {
        var connectionEntityTypeBuilder = modelBuilder.Entity<Connection>();

        connectionEntityTypeBuilder
            .Property(p => p.Type)
            .HasConversion(
                connectionType => connectionType.ToString(),
                value => new ConnectionType(value)
            );

        connectionEntityTypeBuilder.Property(c => c.Name).HasMaxLength(Constraints.ConnectionNameMaximumLength);

        // Unidirectional many-to-many
        connectionEntityTypeBuilder
            .HasMany(t => t.Tags)
            .WithMany()
            .UsingEntity<ConnectionTag>();

        connectionEntityTypeBuilder
            .Property(p => p.Metadata)
            .PersistAsJson();
    }
}

public sealed class ConnectionDbContextSqlite(DbContextOptions<ConnectionDbContextSqlite> options) :
    ConnectionDbContext(options), ISqliteDbContext
{
}

public sealed class ConnectionDbContextPostgres(DbContextOptions<ConnectionDbContextPostgres> options) :
    ConnectionDbContext(options), IPostgresDbContext
{
}

public sealed class ConnectionDbContextPostgresFactory : IDesignTimeDbContextFactory<ConnectionDbContextSqlite>
{
    public ConnectionDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConnectionDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new ConnectionDbContextSqlite(optionsBuilder.Options);
    }
}

public sealed class ConnectionDbContextSqliteFactory : IDesignTimeDbContextFactory<ConnectionDbContextPostgres>
{
    public ConnectionDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConnectionDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new ConnectionDbContextPostgres(optionsBuilder.Options);
    }
}
