using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Persistence;

namespace Burger.Backend.DbContext;

public class BurgerDbContext(DbContextOptions options) : SagaDbContext(options), IBurgerDbContext
{
    internal const string DbSchemaName = "burger";

    public string DefaultSchemaName => DbSchemaName;
    public IEnumerable<Type> NotSynchronizedEntityTypes => [];
    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new OrderBurgerClassMap();
        }
    }

    public Task MigrateAsync(CancellationToken cancellationToken = default)
        => Database.MigrateAsync(cancellationToken);

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchemaName);
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class BurgerDbContextPostgres(DbContextOptions<BurgerDbContextPostgres> options) : BurgerDbContext(options), IPostgresDbContext
{
}

public sealed class BurgerDbContextSqlite(DbContextOptions<BurgerDbContextSqlite> options) : BurgerDbContext(options), ISqliteDbContext
{
}

public sealed class BurgerDbContextPostgresFactory : IDesignTimeDbContextFactory<BurgerDbContextPostgres>
{
    public BurgerDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BurgerDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new BurgerDbContextPostgres(optionsBuilder.Options);
    }
}

public sealed class BurgerDbContextSqliteFactory : IDesignTimeDbContextFactory<BurgerDbContextSqlite>
{
    public BurgerDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BurgerDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new BurgerDbContextSqlite(optionsBuilder.Options);
    }
}
