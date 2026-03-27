using Microsoft.EntityFrameworkCore.Migrations;

namespace Core.OS.Persistence;

/// <summary>
/// A no-op implementation of <see cref="IMigrationsDatabaseLock"/> that skips acquiring
/// the <c>__EFMigrationsLock</c> table lock. This is safe because suite is the sole owner
/// of migrations — only one instance ever migrates a given database.
/// </summary>
internal sealed class NoOpMigrationsDatabaseLock(IHistoryRepository historyRepository) : IMigrationsDatabaseLock
{
    public IHistoryRepository HistoryRepository { get; } = historyRepository;

    IMigrationsDatabaseLock IMigrationsDatabaseLock.ReacquireIfNeeded(bool connectionReopened, bool? transactionRestarted)
        => this;

    Task<IMigrationsDatabaseLock> IMigrationsDatabaseLock.ReacquireIfNeededAsync(
        bool connectionReopened, bool? transactionRestarted, CancellationToken cancellationToken)
        => Task.FromResult<IMigrationsDatabaseLock>(this);

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

