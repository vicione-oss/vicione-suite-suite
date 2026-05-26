using Core.Shared.Connections.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Extensions;
using Sdk.Backend.Persistence;
using Sdk.Connections.Contracts;

namespace Core.OS.DbContext;

/// <remarks>
/// To add migrations see README.md section Database migration
/// </remarks>
[ModuleDbContext(DefaultSchemaName = DbSchemaName)]
public partial class ConnectionDbContext : ModuleDbContext, IConnectionDbContext
{
    internal const string DbSchemaName = "connection";

    public override IEnumerable<Type> NotSynchronizedEntityTypes => [typeof(ConnectionTag)];

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
