using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Core.OS.Persistence;

/// <summary>
///     Publishes replication change sets on behalf of <see cref="ChangeTrackingInterceptor" />.
///     <para>
///         Delivery is bound to the module save that produced the change set: <see cref="Stage" /> runs while the
///         save is still open, <see cref="Commit" /> once it succeeded, and <see cref="Rollback" /> when it did not.
///         A change set is therefore never delivered for data that never reached the database.
///     </para>
/// </summary>
public interface IReplicationPublisher
{
    Task Stage(DbChangeSet changeSet, EfDbContext moduleContext, CancellationToken cancellationToken);

    Task Commit(EfDbContext moduleContext, CancellationToken cancellationToken);

    Task Rollback(EfDbContext moduleContext, CancellationToken cancellationToken);
}
