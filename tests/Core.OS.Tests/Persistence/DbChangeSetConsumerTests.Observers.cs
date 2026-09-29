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
using Sdk.Messaging;

namespace Core.OS.Tests.Persistence;

public partial class DbChangeSetConsumerTests
{
    public sealed class Observers : DbChangeSetConsumerTests, IDisposable
    {
        private static readonly string ContextType = typeof(TestReplicationDbContext).FullName!;

        private readonly ReplicationSequenceTracker _tracker = new(new FakeTimeProvider());
        private readonly SynchronizationState _synchronizationState = new();
        private readonly IReplicationObserver _observer = Substitute.For<IReplicationObserver>();
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly TestReplicationDbContext _dbContext;
        private readonly ServiceProvider _serviceProvider;

        public Observers()
        {
            _synchronizationState.CompleteSynchronization();
            _connection.Open();

            _dbContext = new TestReplicationDbContext(new DbContextOptionsBuilder<TestReplicationDbContext>()
                .UseSqlite(_connection)
                .Options);
            _dbContext.Database.EnsureCreated();

            var services = new ServiceCollection();
            services.AddSingleton(_dbContext);
            services.AddSingleton(new ModuleContextTypeInformation("test-module", typeof(TestReplicationDbContext), ContextType));
            services.AddSingleton(_observer);
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task Should_notify_the_observers_once_the_change_set_is_saved()
        {
            // Arrange
            var consumer = CreateConsumer();
            var savedRowsWhenNotified = -1;
            _observer.When(o => o.ChangeSetApplied(Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>()))
                .Do(_ => savedRowsWhenNotified = _dbContext.Entities.AsNoTracking().Count());

            // Act
            await consumer.Consume(CreateConsumeContext(CreateChangeSet(GoodChange("replicated"))));

            // Assert
            _observer.Received(1).ChangeSetApplied(ContextType,
                Arg.Is<IReadOnlySet<string>>(types => types.SetEquals(new[] { typeof(TestReplicationEntity).FullName! })));
            savedRowsWhenNotified.Should().Be(1);
        }

        [Fact]
        public async Task Should_name_only_the_entity_types_that_were_applied()
        {
            // Arrange
            var consumer = CreateConsumer();
            var badChange = new ChangedEntity(
                """{"Id":"00000000-0000-0000-0000-000000000001"}""",
                "NonExistent.Type",
                "NonExistent.Assembly",
                EntityState.Added);

            // Act
            await consumer.Consume(CreateConsumeContext(CreateChangeSet(badChange, GoodChange("valid"))));

            // Assert
            _observer.Received(1).ChangeSetApplied(ContextType,
                Arg.Is<IReadOnlySet<string>>(types => types.SetEquals(new[] { typeof(TestReplicationEntity).FullName! })));
        }

        [Fact]
        public async Task Should_keep_the_change_set_when_an_observer_fails()
        {
            // Arrange
            var consumer = CreateConsumer();
            _observer.When(o => o.ChangeSetApplied(Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>()))
                .Do(_ => throw new InvalidOperationException("Observer failure"));

            // Act
            var consume = () => consumer.Consume(CreateConsumeContext(CreateChangeSet(GoodChange("kept"))));

            // Assert
            await consume.Should().NotThrowAsync();
            _dbContext.Entities.Should().ContainSingle(e => e.Value == "kept");
        }

        private DbChangeSetConsumer CreateConsumer()
            => new(_serviceProvider, NullLogger<DbChangeSetConsumer>.Instance, _tracker, new ReplicationLagTracker(),
                Substitute.For<ILocalInstanceInformationProvider>(), _synchronizationState);

        private static ChangedEntity GoodChange(string value)
            => new(
                JsonSerializer.Serialize(new TestReplicationEntity { Id = Guid.NewGuid(), Value = value },
                    DefaultJsonSerializerSettings.Default),
                typeof(TestReplicationEntity).FullName!,
                typeof(TestReplicationEntity).Assembly.FullName,
                EntityState.Added);

        private static DbChangeSet CreateChangeSet(params ChangedEntity[] changes)
            => new([.. changes], ContextType, 1, DateTimeOffset.UtcNow);

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
            _serviceProvider.Dispose();
        }
    }
}
