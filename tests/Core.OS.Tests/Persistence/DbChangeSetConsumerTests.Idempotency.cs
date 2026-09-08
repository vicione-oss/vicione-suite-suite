using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Core.OS.Persistence.Consumers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Messaging;

namespace Core.OS.Tests.Persistence;

public partial class DbChangeSetConsumerTests
{
    /// <summary>
    /// ADR-002: the replication consumer applies change sets with upsert semantics so that a
    /// redelivered change set (connectivity loss, broker redelivery, full-sync overlap) produces
    /// the same local state instead of a primary key violation.
    /// </summary>
    public sealed class Idempotency : DbChangeSetConsumerTests, IDisposable
    {
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly ReplicationSequenceTracker _tracker;
        private readonly ILocalInstanceInformationProvider _localInstanceInfo = Substitute.For<ILocalInstanceInformationProvider>();
        private readonly SynchronizationState _synchronizationState = new();
        private readonly SqliteConnection _connection;
        private readonly TestReplicationDbContext _dbContext;
        private readonly ServiceProvider _serviceProvider;

        public Idempotency()
        {
            _tracker = new ReplicationSequenceTracker(_timeProvider);
            _synchronizationState.CompleteSynchronization();

            var instanceInfo = Substitute.For<IInstanceInformation>();
            instanceInfo.Id.Returns(Guid.NewGuid());
            instanceInfo.Type.Returns(InstanceType.Slave);
            instanceInfo.Name.Returns("TestSlave");
            _localInstanceInfo.Local.Returns(instanceInfo);
            _localInstanceInfo.LoadedModules.Returns(new List<string> { "TestModule" });

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<TestReplicationDbContext>()
                .UseSqlite(_connection)
                .Options;
            _dbContext = new TestReplicationDbContext(options);
            _dbContext.Database.EnsureCreated();

            var services = new ServiceCollection();
            services.AddSingleton(_dbContext);
            services.AddSingleton(new ModuleContextTypeInformation(
                "test-module",
                typeof(TestReplicationDbContext),
                typeof(TestReplicationDbContext).FullName!));
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task Should_update_existing_row_when_added_change_is_redelivered()
        {
            // Arrange — the same entity is announced twice as Added, e.g. after a full-sync overlap.
            var consumer = CreateConsumer();
            var entityId = Guid.NewGuid();
            var first = CreateChangeSet(new TestReplicationEntity { Id = entityId, Value = "first" }, EntityState.Added, 1);
            var second = CreateChangeSet(new TestReplicationEntity { Id = entityId, Value = "second" }, EntityState.Added, 2);

            // Act
            await consumer.Consume(CreateConsumeContext(first));
            await consumer.Consume(CreateConsumeContext(second));

            // Assert
            _dbContext.Entities.Should().HaveCount(1);
            _dbContext.Entities.Single().Value.Should().Be("second");
        }

        [Fact]
        public async Task Should_insert_row_when_modified_change_arrives_for_missing_row()
        {
            // Arrange — a Modified change may arrive before the row exists locally.
            var consumer = CreateConsumer();
            var entity = new TestReplicationEntity { Id = Guid.NewGuid(), Value = "modified" };
            var changeSet = CreateChangeSet(entity, EntityState.Modified, 1);

            // Act
            await consumer.Consume(CreateConsumeContext(changeSet));

            // Assert
            _dbContext.Entities.Should().ContainSingle(e => e.Id == entity.Id && e.Value == "modified");
        }

        [Fact]
        public async Task Should_not_throw_when_deleted_change_is_redelivered()
        {
            // Arrange
            var consumer = CreateConsumer();
            var entity = new TestReplicationEntity { Id = Guid.NewGuid(), Value = "doomed" };
            await consumer.Consume(CreateConsumeContext(CreateChangeSet(entity, EntityState.Added, 1)));

            // Act — the delete is applied twice; the second one finds nothing to remove.
            await consumer.Consume(CreateConsumeContext(CreateChangeSet(entity, EntityState.Deleted, 2)));
            var act = () => consumer.Consume(CreateConsumeContext(CreateChangeSet(entity, EntityState.Deleted, 3)));

            // Assert
            await act.Should().NotThrowAsync();
            _dbContext.Entities.Should().BeEmpty();
        }

        private DbChangeSetConsumer CreateConsumer()
            => new(_serviceProvider, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);

        private static DbChangeSet CreateChangeSet(TestReplicationEntity entity, EntityState state, long sequenceNumber)
            => new(
                [
                    new ChangedEntity(
                        JsonSerializer.Serialize(entity, DefaultJsonSerializerSettings.Default),
                        typeof(TestReplicationEntity).FullName!,
                        typeof(TestReplicationEntity).Assembly.FullName,
                        state)
                ],
                typeof(TestReplicationDbContext).FullName!,
                sequenceNumber,
                DateTimeOffset.UtcNow);

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
            _serviceProvider.Dispose();
        }
    }
}
