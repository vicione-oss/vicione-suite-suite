using System.Text.Json;
using AwesomeAssertions;
using Core.OS.Instance;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Services;
using Core.OS.Persistence;
using Core.OS.Persistence.Consumers;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Sdk.Backend.Persistence;
using Sdk.Instance;
using Sdk.Messaging;
using Xunit;

namespace Core.OS.Tests.Persistence;

public partial class DbChangeSetConsumerTests
{
    public sealed class ErrorIsolation : DbChangeSetConsumerTests, IDisposable
    {
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly ReplicationSequenceTracker _tracker;
        private readonly ILocalInstanceInformationProvider _localInstanceInfo = Substitute.For<ILocalInstanceInformationProvider>();
        private readonly ILogger<DbChangeSetConsumer> _logger = Substitute.For<ILogger<DbChangeSetConsumer>>();
        private readonly SynchronizationState _synchronizationState = new();
        private readonly SqliteConnection _connection;
        private readonly TestReplicationDbContext _dbContext;
        private readonly ServiceProvider _serviceProvider;

        public ErrorIsolation()
        {
            _tracker = new ReplicationSequenceTracker(_timeProvider);
            _synchronizationState.CompleteSynchronization();
            _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

            var instanceInfo = Substitute.For<IInstanceInformation>();
            instanceInfo.Id.Returns(Guid.NewGuid());
            instanceInfo.Type.Returns(InstanceType.Slave);
            instanceInfo.Name.Returns("TestSlave");
            instanceInfo.SerialNumber.Returns("SN001");
            instanceInfo.SystemType.Returns("Edge-S");
            instanceInfo.SdkVersion.Returns("1.0.0");
            instanceInfo.Version.Returns("1.0.0");
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
            services.AddSingleton<TestReplicationDbContext>(_dbContext);
            services.AddSingleton(new ModuleContextTypeInformation(
                "test-module",
                typeof(TestReplicationDbContext),
                typeof(TestReplicationDbContext).FullName!));
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task ApplyChangeSet_should_apply_good_entities_when_one_entity_fails()
        {
            // Arrange
            var consumer = CreateConsumer();
            var goodEntity = new TestReplicationEntity { Id = Guid.NewGuid(), Value = "valid" };
            var goodChange = CreateChangedEntity(goodEntity, EntityState.Added);
            var badChange = new ChangedEntity(
                """{"Id":"00000000-0000-0000-0000-000000000001","Value":"bad"}""",
                "NonExistent.Namespace.FakeEntityType",
                "NonExistent.Assembly",
                EntityState.Added);

            var changeSet = new DbChangeSet([badChange, goodChange], typeof(TestReplicationDbContext).FullName!, 1, DateTimeOffset.UtcNow);

            // Act
            await consumer.Consume(CreateConsumeContext(changeSet));

            // Assert
            _dbContext.Entities.Should().HaveCount(1);
            _dbContext.Entities.Single().Value.Should().Be("valid");
        }

        [Fact]
        public async Task ApplyChangeSet_should_log_error_when_entity_fails()
        {
            // Arrange
            var consumer = CreateConsumer();
            var goodEntity = new TestReplicationEntity { Id = Guid.NewGuid(), Value = "valid" };
            var goodChange = CreateChangedEntity(goodEntity, EntityState.Added);
            var badChange = new ChangedEntity(
                """{"Id":"00000000-0000-0000-0000-000000000001","Value":"bad"}""",
                "NonExistent.Namespace.FakeEntityType",
                "NonExistent.Assembly",
                EntityState.Added);

            var changeSet = new DbChangeSet([badChange, goodChange], typeof(TestReplicationDbContext).FullName!, 1, DateTimeOffset.UtcNow);

            // Act
            await consumer.Consume(CreateConsumeContext(changeSet));

            // Assert — verify error was logged for the bad entity
            var logCalls = _logger.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == "Log")
                .ToList();

            logCalls.Should().NotBeEmpty();
            var args = logCalls[0].GetArguments();
            args[0].Should().Be(LogLevel.Error);
        }

        [Fact]
        public async Task ApplyChangeSet_should_throw_when_all_entities_fail()
        {
            // Arrange
            var consumer = CreateConsumer();
            var badChange1 = new ChangedEntity(
                """{"Id":"00000000-0000-0000-0000-000000000001"}""",
                "NonExistent.Type1",
                "NonExistent.Assembly",
                EntityState.Added);
            var badChange2 = new ChangedEntity(
                """{"Id":"00000000-0000-0000-0000-000000000002"}""",
                "NonExistent.Type2",
                "NonExistent.Assembly",
                EntityState.Added);

            var changeSet = new DbChangeSet([badChange1, badChange2], typeof(TestReplicationDbContext).FullName!, 1, DateTimeOffset.UtcNow);

            // Act
            var act = () => consumer.Consume(CreateConsumeContext(changeSet));

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("All 2 entities*failed to apply*");
        }

        [Fact]
        public async Task ApplyChangeSet_should_skip_null_entity_and_apply_good_ones()
        {
            // Arrange
            var consumer = CreateConsumer();
            var goodEntity = new TestReplicationEntity { Id = Guid.NewGuid(), Value = "survives" };
            var goodChange = CreateChangedEntity(goodEntity, EntityState.Added);
            var nullChange = new ChangedEntity(
                null,
                typeof(TestReplicationEntity).FullName!,
                typeof(TestReplicationEntity).Assembly.FullName,
                EntityState.Added);

            var changeSet = new DbChangeSet([nullChange, goodChange], typeof(TestReplicationDbContext).FullName!, 1, DateTimeOffset.UtcNow);

            // Act
            await consumer.Consume(CreateConsumeContext(changeSet));

            // Assert
            _dbContext.Entities.Should().HaveCount(1);
            _dbContext.Entities.Single().Value.Should().Be("survives");
        }

        [Fact]
        public async Task ApplyChangeSet_should_apply_all_good_entities_when_one_fails()
        {
            // Arrange
            var consumer = CreateConsumer();
            var entity1 = new TestReplicationEntity { Id = Guid.NewGuid(), Value = "first" };
            var entity2 = new TestReplicationEntity { Id = Guid.NewGuid(), Value = "second" };
            var goodChange1 = CreateChangedEntity(entity1, EntityState.Added);
            var goodChange2 = CreateChangedEntity(entity2, EntityState.Added);
            var badChange = new ChangedEntity(
                """{"Id":"00000000-0000-0000-0000-000000000001"}""",
                "NonExistent.Type",
                "NonExistent.Assembly",
                EntityState.Added);

            var changeSet = new DbChangeSet([goodChange1, badChange, goodChange2], typeof(TestReplicationDbContext).FullName!, 1, DateTimeOffset.UtcNow);

            // Act
            await consumer.Consume(CreateConsumeContext(changeSet));

            // Assert
            _dbContext.Entities.Should().HaveCount(2);
            _dbContext.Entities.Select(e => e.Value).Should().BeEquivalentTo(["first", "second"]);
        }

        private DbChangeSetConsumer CreateConsumer()
            => new(_serviceProvider, _logger, _tracker, new ReplicationLagTracker(), _localInstanceInfo, _synchronizationState);

        private static ChangedEntity CreateChangedEntity(TestReplicationEntity entity, EntityState state)
            => new(
                JsonSerializer.Serialize(entity, DefaultJsonSerializerSettings.Default),
                typeof(TestReplicationEntity).FullName!,
                typeof(TestReplicationEntity).Assembly.FullName,
                state);

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
            _serviceProvider.Dispose();
        }
    }
}

public class TestReplicationEntity
{
    public Guid Id { get; set; }
    public string Value { get; set; } = "";
}

public class TestReplicationDbContext(DbContextOptions<TestReplicationDbContext> options) : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public DbSet<TestReplicationEntity> Entities => Set<TestReplicationEntity>();
}
