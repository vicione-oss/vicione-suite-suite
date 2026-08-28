using Core.OS.DbContext;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Middleware.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Core.OS.Persistence;

/// <summary>
///     Stages the change set in MassTransit's Bus Outbox on the module's own connection and transaction, so the
///     outbox rows and the business data commit or roll back together (ADR-003 Gap 1).
///     <para>
///         The Bus Outbox only ever writes to the change tracker of the <see cref="OutboxDbContext" /> it was given —
///         nothing is delivered unless that instance is saved. Enlisting it in the module's transaction is what turns
///         "published" into "published if and only if the data was written".
///     </para>
/// </summary>
internal sealed class BusOutboxReplicationPublisher(IServiceProvider services) : IReplicationPublisher
{
    private readonly Dictionary<EfDbContext, PendingDelivery> _pending = [];

    public async Task Stage(DbChangeSet changeSet, EfDbContext moduleContext, CancellationToken cancellationToken)
    {
        // The outbox context is opened on the module's own connection, which only works while that connection is
        // Npgsql. Only the master replicates and the master is always on PostgreSQL, so anything else is a
        // misconfiguration to stay out of rather than a case to handle.
        if (!moduleContext.Database.IsNpgsql())
            return;

        await PersistSequenceNumber(changeSet, cancellationToken);

        var delivery = await PendingDelivery.Enlist(services, moduleContext, cancellationToken);
        _pending[moduleContext] = delivery;

        await delivery.Stage(changeSet, cancellationToken);
    }

    public async Task Commit(EfDbContext moduleContext, CancellationToken cancellationToken)
    {
        if (_pending.Remove(moduleContext, out var delivery))
            await delivery.Commit(cancellationToken);
    }

    public async Task Rollback(EfDbContext moduleContext, CancellationToken cancellationToken)
    {
        if (_pending.Remove(moduleContext, out var delivery))
            await delivery.Rollback(cancellationToken);
    }

    /// <summary>
    ///     Runs on a connection of its own, deliberately outside the business transaction: the counter must stay
    ///     ahead of every delivered sequence number even when the transaction rolls back. A counter that is ahead
    ///     by one is harmless — slaves handle gaps via the reorder buffer and timeout-triggered resync (ADR-003 Gap 4).
    /// </summary>
    private async Task PersistSequenceNumber(DbChangeSet changeSet, CancellationToken cancellationToken)
        => await services.GetRequiredService<OutboxDbContext>().Database.ExecuteSqlRawAsync("""
            INSERT INTO outbox."ReplicationSequenceState" ("ContextType", "LastSequenceNumber")
            VALUES ({0}, {1})
            ON CONFLICT ("ContextType") DO UPDATE SET "LastSequenceNumber" = {1}
            """, [changeSet.ContextType, changeSet.SequenceNumber], cancellationToken);

    private sealed class PendingDelivery(
        EntityFrameworkScopedBusContext<IBus, OutboxDbContext> busContext,
        OutboxDbContext outboxContext,
        IDbContextTransaction transaction,
        bool ownsTransaction)
    {
        // The delivery owns both objects for as long as the save runs; Commit and Rollback always release them.
#pragma warning disable CA2000
        public static async Task<PendingDelivery> Enlist(IServiceProvider services, EfDbContext moduleContext, CancellationToken cancellationToken)
        {
            var ownsTransaction = moduleContext.Database.CurrentTransaction is null;
            var transaction = moduleContext.Database.CurrentTransaction
                ?? await moduleContext.Database.BeginTransactionAsync(cancellationToken);

            var outboxContext = new OutboxDbContext(new DbContextOptionsBuilder<OutboxDbContext>()
                .UseNpgsql(moduleContext.Database.GetDbConnection())
                .Options);

            try
            {
                await outboxContext.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

                var busContext = new EntityFrameworkScopedBusContext<IBus, OutboxDbContext>(
                    services.GetRequiredService<IBus>(),
                    outboxContext,
                    services.GetRequiredService<IBusOutboxNotification>(),
                    services.GetRequiredService<IClientFactory>(),
                    services);

                return new PendingDelivery(busContext, outboxContext, transaction, ownsTransaction);
            }
            catch
            {
                await outboxContext.DisposeAsync();
                throw;
            }
        }
#pragma warning restore CA2000

        public async Task Stage(DbChangeSet changeSet, CancellationToken cancellationToken)
        {
            await busContext.PublishEndpoint.Publish(changeSet, cancellationToken);
            await outboxContext.SaveChangesAsync(cancellationToken);
        }

        public async Task Commit(CancellationToken cancellationToken)
        {
            if (ownsTransaction)
                await transaction.CommitAsync(cancellationToken);

            await Release();
        }

        public async Task Rollback(CancellationToken cancellationToken)
        {
            if (ownsTransaction)
                await transaction.RollbackAsync(cancellationToken);

            await Release();
        }

        /// <summary>
        ///     Disposing the bus context tells the delivery service to pick the message up right away instead of on
        ///     its next query interval. After a rollback that wakes it for nothing, which costs one empty query.
        /// </summary>
        private async Task Release()
        {
            busContext.Dispose();
            await outboxContext.DisposeAsync();

            if (ownsTransaction)
                await transaction.DisposeAsync();
        }
    }
}
