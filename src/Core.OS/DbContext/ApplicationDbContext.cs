using Core.Shared.Instance.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Extensions;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext;

public class ApplicationDbContext(DbContextOptions options) : ModuleDbContext(options), IApplicationDbContext
{
    internal const string DbSchemaName = "app";

    public DbSet<InstanceInformation> InstanceInfo => Set<InstanceInformation>();
    public DbSet<CrossInstanceConfiguration> CrossInstanceConfiguration => Set<CrossInstanceConfiguration>();
    public DbSet<Nonce> Nonces => Set<Nonce>();
    public DbSet<OnboardingState> OnboardingStates => Set<OnboardingState>();

    public override string DefaultSchemaName => DbSchemaName;

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<InstanceInformation>()
            .Property(i => i.InstalledModules)
            .PersistAsJson();
}

public sealed class ApplicationDbContextSqlite(DbContextOptions<ApplicationDbContextSqlite> options) :
    ApplicationDbContext(options), ISqliteDbContext
{
}

public sealed class ApplicationDbContextPostgres(DbContextOptions<ApplicationDbContextPostgres> options) :
    ApplicationDbContext(options), IPostgresDbContext
{
}

public sealed class ApplicationDbContextSqliteFactory : IDesignTimeDbContextFactory<ApplicationDbContextSqlite>
{
    public ApplicationDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new ApplicationDbContextSqlite(optionsBuilder.Options);
    }
}

public sealed class ApplicationDbContextPostgresFactory : IDesignTimeDbContextFactory<ApplicationDbContextPostgres>
{
    public ApplicationDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new ApplicationDbContextPostgres(optionsBuilder.Options);
    }
}
