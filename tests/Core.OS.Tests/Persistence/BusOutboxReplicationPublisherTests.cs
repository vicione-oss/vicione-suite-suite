using AwesomeAssertions;
using Core.OS.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Core.OS.Tests.Persistence;

/// <summary>
/// The transaction the publisher shares with the module save cannot be stood in for, so the delivery guarantees
/// themselves are pinned in <see cref="BusOutboxReplicationPublisherFacts" /> against a real PostgreSQL. What is
/// left is everything the publisher must do without touching a database: the interceptor reports every save,
/// including the majority that never staged a change set.
/// <para>
/// The empty service provider is the assertion — reaching for the outbox at all would throw.
/// </para>
/// </summary>
public sealed class BusOutboxReplicationPublisherTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_ignore_a_commit_for_a_save_that_staged_nothing()
    {
        // Arrange
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var moduleContext = CreateModuleContext();
        var publisher = new BusOutboxReplicationPublisher(services);

        // Act
        var commit = async () => await publisher.Commit(moduleContext, _cancellationToken);

        // Assert
        await commit.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_ignore_a_rollback_for_a_save_that_staged_nothing()
    {
        // Arrange
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var moduleContext = CreateModuleContext();
        var publisher = new BusOutboxReplicationPublisher(services);

        // Act
        var rollback = async () => await publisher.Rollback(moduleContext, _cancellationToken);

        // Assert
        await rollback.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_skip_staging_when_the_module_is_not_on_postgres()
    {
        // Arrange
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var moduleContext = CreateModuleContext();
        var publisher = new BusOutboxReplicationPublisher(services);

        // Act
        var stage = async () => await publisher.Stage(CreateChangeSet(), moduleContext, _cancellationToken);

        // Assert
        await stage.Should().NotThrowAsync();
    }

    private static DbChangeSet CreateChangeSet()
        => new([new ChangedEntity("{}", "Entity", "Assembly", EntityState.Added)],
            "ContextType",
            1,
            DateTimeOffset.UnixEpoch);

    private static EfDbContext CreateModuleContext()
        => new(new DbContextOptionsBuilder().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
