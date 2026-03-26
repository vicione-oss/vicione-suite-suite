using Core.Shared.Instance.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Extensions;
using Sdk.Backend.Persistence;

namespace Core.OS.DbContext;

[ModuleDbContext(DefaultSchemaName = DbSchemaName)]
public partial class ApplicationDbContext(DbContextOptions options) : ModuleDbContext(options), IApplicationDbContext
{
    internal const string DbSchemaName = "app";

    public override IEnumerable<Type> NotSynchronizedEntityTypes => [typeof(Nonce)];

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstanceInformation>()
            .Property(i => i.InstalledModules)
            .PersistAsJson();
    }
}
