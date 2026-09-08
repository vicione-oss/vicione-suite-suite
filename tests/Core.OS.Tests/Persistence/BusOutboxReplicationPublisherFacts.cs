using Core.Module;
using Core.OS.DbContext;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Persistence;
using Core.OS.Tests.Extensions;
using Core.Shared.Instance.HealthCheck;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Core.OS.Tests.Persistence;

/// <summary>
/// The Bus Outbox stages into a change tracker; nothing is delivered unless that instance is saved, and it is only
/// safe to deliver once the business data is committed. <see cref="BusOutboxReplicationPublisher" /> gets both by
/// enlisting an <see cref="OutboxDbContext" /> in the module's own connection and transaction (ADR-003 Gap 1).
/// <para>
/// Needs a real PostgreSQL — the behaviour under test is the shared transaction, which no in-memory or SQLite
/// provider can stand in for. CI provides one as a service container; see docs/integration-testing.md.
/// </para>
/// </summary>
public sealed class BusOutboxReplicationPublisherFacts
{
    private static readonly string PostgresConnection = PostgresTestConnection.ForDatabase("replicationoutbox");

    [Fact]
    [Trait(Traits.Category, Traits.Integration)]
    public async Task Should_deliver_the_change_set_when_the_module_save_commits()
    {
        // Arrange
        await using var outboxDbContext = InitializeOutboxDbContext();
        await using var moduleDbContext = CreateModuleDbContext();
        await using var serviceProvider = SetupMaster();
        await using var scope = serviceProvider.CreateAsyncScope();
        var publisher = new BusOutboxReplicationPublisher(scope.ServiceProvider);

        // Act
        await publisher.Stage(CreateChangeSet(), moduleDbContext, TestContext.Current.CancellationToken);
        await publisher.Commit(moduleDbContext, TestContext.Current.CancellationToken);

        // Assert
        var staged = await outboxDbContext.Set<OutboxMessage>().CountAsync(TestContext.Current.CancellationToken);
        staged.Should().Be(1);
    }

    [Fact]
    [Trait(Traits.Category, Traits.Integration)]
    public async Task Should_drop_the_change_set_when_the_module_save_rolls_back()
    {
        // Arrange
        await using var outboxDbContext = InitializeOutboxDbContext();
        await using var moduleDbContext = CreateModuleDbContext();
        await using var serviceProvider = SetupMaster();
        await using var scope = serviceProvider.CreateAsyncScope();
        var publisher = new BusOutboxReplicationPublisher(scope.ServiceProvider);

        // Act
        await publisher.Stage(CreateChangeSet(), moduleDbContext, TestContext.Current.CancellationToken);
        await publisher.Rollback(moduleDbContext, TestContext.Current.CancellationToken);

        // Assert
        var staged = await outboxDbContext.Set<OutboxMessage>().CountAsync(TestContext.Current.CancellationToken);
        staged.Should().Be(0);
    }

    [Fact]
    [Trait(Traits.Category, Traits.Integration)]
    public async Task Should_keep_the_sequence_number_when_the_module_save_rolls_back()
    {
        // Arrange — the counter must stay ahead of every delivered sequence number even when the data is
        // discarded, so slaves can detect the gap instead of silently missing a change set (ADR-003 Gap 4).
        await using var outboxDbContext = InitializeOutboxDbContext();
        await using var moduleDbContext = CreateModuleDbContext();
        await using var serviceProvider = SetupMaster();
        await using var scope = serviceProvider.CreateAsyncScope();
        var publisher = new BusOutboxReplicationPublisher(scope.ServiceProvider);

        // Act
        await publisher.Stage(CreateChangeSet(), moduleDbContext, TestContext.Current.CancellationToken);
        await publisher.Rollback(moduleDbContext, TestContext.Current.CancellationToken);

        // Assert
        var persisted = await outboxDbContext.ReplicationSequenceStates
            .SingleAsync(state => state.ContextType == "ContextType", TestContext.Current.CancellationToken);
        persisted.LastSequenceNumber.Should().Be(1);
    }

    private static DbChangeSet CreateChangeSet()
        => new([new ChangedEntity("{}", "Entity", "Assembly", EntityState.Added)],
            "ContextType",
            1,
            DateTimeOffset.UnixEpoch);

    /// <summary>
    ///     Stands in for a module context. The publisher only ever asks it for a connection and a transaction, so an
    ///     empty model on the same database is enough — and keeps the fact independent of any module's mapping.
    /// </summary>
    private static EfDbContext CreateModuleDbContext()
        => new(new DbContextOptionsBuilder().UseNpgsql(PostgresConnection).Options);

    private static OutboxDbContext InitializeOutboxDbContext()
    {
        var dbContext = new OutboxDbContext(new DbContextOptionsBuilder<OutboxDbContext>()
            .UseNpgsql(PostgresConnection)
            .Options);

        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        return dbContext;
    }

    private static ServiceProvider SetupMaster()
    {
        var config = new TestConfig()
            .UseInMemoryBus(false)
            .UseInstanceType(InstanceType.Master)
            .BuildConfiguration();

        var moduleHost = Substitute.For<IModuleHost>();
        moduleHost.GetContext()
            .Returns(new SuiteDependencyContext(new ModuleDependencyContext(ModuleType.Backend, "jsonPath", true), null, []));

        var connectionStringProvider = Substitute.For<IMasterDbConnectionStringProvider>();
        connectionStringProvider.ConnectionString.Returns(PostgresConnection);

        var masterHealthInfo = Substitute.For<IMasterHealthInfo>();
        masterHealthInfo.IsMasterReachable.Returns(true);

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(moduleHost)
            .AddSingleton(config)
            .AddSingleton(connectionStringProvider)
            .AddSingleton(masterHealthInfo)
            .AddInstanceServicesMock(InstanceType.Master)
            .AddMassTransitMessageBus(config, _ => { }, [])
            .BuildServiceProvider();
    }
}
