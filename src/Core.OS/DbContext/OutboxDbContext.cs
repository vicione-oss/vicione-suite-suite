using Core.OS.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Core.OS.DbContext;

/// <summary>
/// Dedicated DbContext for MassTransit Bus Outbox tables.
/// Shares the same PostgreSQL database as the business DbContexts.
/// The outbox tables are stored in a dedicated "outbox" schema.
/// </summary>
public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : Microsoft.EntityFrameworkCore.DbContext(options)
{
    private const string DbSchemaName = "outbox";

    public DbSet<ReplicationSequenceState> ReplicationSequenceStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DbSchemaName);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<ReplicationSequenceState>(entity =>
        {
            entity.ToTable("ReplicationSequenceState");
            entity.HasKey(e => e.ContextType);
            entity.Property(e => e.ContextType).HasMaxLength(512);
        });
    }
}

public sealed class OutboxDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OutboxDbContext>
{
    public OutboxDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OutboxDbContext>();
        optionsBuilder.UseNpgsql();

        return new OutboxDbContext(optionsBuilder.Options);
    }
}
