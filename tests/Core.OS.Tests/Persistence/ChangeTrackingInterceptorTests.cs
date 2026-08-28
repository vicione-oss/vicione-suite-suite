using System.Text.Json;
using Core.OS.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sdk.Backend.Persistence;
using Sdk.Messaging;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace Core.OS.Tests.Persistence;

public class ChangeTrackingInterceptorTests
{
    private readonly CancellationToken _cancellationToken;
    private readonly ChangeTrackingInterceptor _interceptor;
    private readonly IReplicationPublisher _publisher;
    private readonly InterceptionResult<int> _result;
    private readonly ReplicationSequenceCounter _sequenceCounter;

    public ChangeTrackingInterceptorTests()
    {
        _publisher = Substitute.For<IReplicationPublisher>();
        _sequenceCounter = new ReplicationSequenceCounter();
        _interceptor = new ChangeTrackingInterceptor(_publisher, _sequenceCounter);
        _result = InterceptionResult<int>.SuppressWithResult(1);
        _cancellationToken = CancellationToken.None;
    }

    public sealed class SavingChangesAsync : ChangeTrackingInterceptorTests
    {
        [Fact]
        public async Task Should_skip_non_module_db_context()
        {
            // Arrange
            var eventData = new DbContextEventData(default!, default!, null);

            // Act
            var returnedResult = await _interceptor.SavingChangesAsync(eventData, _result, _cancellationToken);

            // Assert
            returnedResult.Should().Be(_result);
        }

        [Fact]
        public async Task Should_stage_added_entity()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            var entity = new TestType
            {
                Id = 1,
                SubType = new SubType
                {
                    Id = 1
                }
            };
            var entityType = typeof(TestType);
            var expectedChange = new ChangedEntity(JsonSerializer.Serialize(entity, DefaultJsonSerializerSettings.Default),
                entityType.FullName ?? entityType.Name,
                entityType.Assembly.FullName,
                EntityState.Added);

            // Act
            context.TestTypes.Add(entity);
            var result = await context.SaveChangesAsync(_cancellationToken);

            // Assert
            _ = _publisher.Received()
                .Stage(
                    Arg.Is<DbChangeSet>(m => m!.Changes.Count == 1
                        && m.Changes.First().EntityTypeFullName == expectedChange.EntityTypeFullName
                        && m.Changes.First().AssemblyFullName == expectedChange.AssemblyFullName
                        && m.Changes.First().State == expectedChange.State
                        && m.Changes.First().Entity == expectedChange.Entity),
                    Arg.Any<EfDbContext>(),
                    _cancellationToken);
            result.Should().Be(2);
        }

        [Fact]
        public async Task Should_stage_before_the_save_and_commit_after()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            context.TestTypes.Add(new TestType { Id = 1, SubType = new SubType { Id = 1 } });

            // Act
            var result = await context.SaveChangesAsync(_cancellationToken);

            // Assert — staging while the save is still open and committing only afterwards is what keeps a change
            // set from being delivered for data that never reached the database (ADR-003 Gap 1).
            Received.InOrder(() =>
            {
                _ = _publisher.Stage(Arg.Any<DbChangeSet>(), Arg.Any<EfDbContext>(), _cancellationToken);
                _ = _publisher.Commit(Arg.Any<EfDbContext>(), _cancellationToken);
            });
            result.Should().Be(2);
        }

        [Fact]
        public async Task Should_exclude_blacklisted_entity_from_changeset()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            var blacklistedEntity = new BlacklistedTestType
            {
                Id = 1,
                SubType = new SubType { Id = 1 }
            };
            var entity = new TestType
            {
                Id = 1,
                SubType = new SubType { Id = 1 }
            };
            var entityType = typeof(TestType);
            var expectedChange = new ChangedEntity(JsonSerializer.Serialize(entity, DefaultJsonSerializerSettings.Default),
                entityType.FullName ?? entityType.Name,
                entityType.Assembly.FullName,
                EntityState.Added);

            // Act
            context.BlacklistedTestTypes.Add(blacklistedEntity);
            context.TestTypes.Add(entity);
            var result = await context.SaveChangesAsync(_cancellationToken);

            // Assert
            _ = _publisher.Received()
                .Stage(
                    Arg.Is<DbChangeSet>(m => m!.Changes.Count == 1
                        && m.Changes.First().EntityTypeFullName == expectedChange.EntityTypeFullName
                        && m.Changes.First().AssemblyFullName == expectedChange.AssemblyFullName
                        && m.Changes.First().State == expectedChange.State
                        && m.Changes.First().Entity == expectedChange.Entity),
                    Arg.Any<EfDbContext>(),
                    _cancellationToken);
            result.Should().Be(4);
        }

        [Fact]
        public async Task Should_not_stage_when_only_blacklisted_entities()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            var entity = new BlacklistedTestType
            {
                Id = 1,
                SubType = new SubType { Id = 1 }
            };

            // Act
            context.BlacklistedTestTypes.Add(entity);
            var result = await context.SaveChangesAsync(_cancellationToken);

            // Assert
            _ = _publisher.DidNotReceive().Stage(Arg.Any<DbChangeSet>(), Arg.Any<EfDbContext>(), Arg.Any<CancellationToken>());
            result.Should().Be(2);
        }

        [Fact]
        public async Task Should_stage_modified_entity()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            var entity = new TestType { Id = 1, SubType = new SubType { Id = 1 } };
            context.TestTypes.Add(entity);
            await context.SaveChangesAsync(_cancellationToken);
            _publisher.ClearReceivedCalls();

            // Act
            context.Entry(entity).State = EntityState.Modified;
            await context.SaveChangesAsync(_cancellationToken);

            // Assert
            _ = _publisher.Received(1)
                .Stage(
                    Arg.Is<DbChangeSet>(m => m!.Changes.Count == 1
                        && m.Changes.First().State == EntityState.Modified),
                    Arg.Any<EfDbContext>(),
                    _cancellationToken);
        }

        [Fact]
        public async Task Should_stage_deleted_entity()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            var entity = new TestType { Id = 1, SubType = new SubType { Id = 1 } };
            context.TestTypes.Add(entity);
            await context.SaveChangesAsync(_cancellationToken);
            _publisher.ClearReceivedCalls();

            // Act
            context.TestTypes.Remove(entity);
            await context.SaveChangesAsync(_cancellationToken);

            // Assert
            _ = _publisher.Received(1)
                .Stage(
                    Arg.Is<DbChangeSet>(m => m!.Changes.Count == 1
                        && m.Changes.First().State == EntityState.Deleted),
                    Arg.Any<EfDbContext>(),
                    _cancellationToken);
        }

        [Fact]
        public async Task Should_propagate_staging_failure()
        {
            // Arrange — the publisher throws to simulate an unreachable outbox
            _publisher
                .Stage(Arg.Any<DbChangeSet>(), Arg.Any<EfDbContext>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new InvalidOperationException("Broker unreachable")));

            await using var context = TestDbContext.CreateContext(_publisher);
            var entity = new TestType { Id = 1, SubType = new SubType { Id = 1 } };
            context.TestTypes.Add(entity);

            // Act
            var act = () => context.SaveChangesAsync(_cancellationToken);

            // Assert — exception propagates, preventing the save from completing
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    public sealed class SaveChangesFailedAsync : ChangeTrackingInterceptorTests
    {
        [Fact]
        public async Task Should_roll_the_staged_change_set_back()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            var eventData = new DbContextErrorEventData(default!, default!, context, new InvalidOperationException("Save failed"));

            // Act
            await _interceptor.SaveChangesFailedAsync(eventData, _cancellationToken);

            // Assert
            _ = _publisher.Received(1).Rollback(context, _cancellationToken);
        }
    }

    public sealed class SaveChangesCanceledAsync : ChangeTrackingInterceptorTests
    {
        [Fact]
        public async Task Should_roll_the_staged_change_set_back()
        {
            // Arrange
            await using var context = TestDbContext.CreateContext(_publisher);
            var eventData = new DbContextEventData(default!, default!, context);

            // Act
            await _interceptor.SaveChangesCanceledAsync(eventData, _cancellationToken);

            // Assert
            _ = _publisher.Received(1).Rollback(context, _cancellationToken);
        }
    }

    public sealed class SavingChanges : ChangeTrackingInterceptorTests
    {
        [Fact]
        public void Should_throw_invalid_operation_exception()
        {
            // Arrange
            var eventData = new DbContextEventData(default!, default!, null);

            // Act + Assert
            var act = () => _interceptor.SavingChanges(eventData, _result);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Do not save changes synchronously. Use 'SaveChangesAsync' instead!");
        }
    }

    private interface ITestDbContext : IModuleDbContext
    {
        DbSet<TestType> TestTypes { get; }

        DbSet<BlacklistedTestType> BlacklistedTestTypes { get; }
    }

    private class TestDbContext(DbContextOptions<TestDbContext> dbContextOptions) :
        ModuleDbContext(dbContextOptions), ITestDbContext
    {
        public override string DefaultSchemaName => "Schema";
        public override IEnumerable<Type> NotSynchronizedEntityTypes =>
        [
            typeof(BlacklistedTestType)
        ];

        public DbSet<TestType> TestTypes => Set<TestType>();
        public DbSet<BlacklistedTestType> BlacklistedTestTypes => Set<BlacklistedTestType>();

        protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestType>().OwnsOne(t => t.SubType);
            modelBuilder.Entity<BlacklistedTestType>().OwnsOne(t => t.SubType);
        }

        public static TestDbContext CreateContext(IReplicationPublisher publisher)
        {
            var builder = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new ChangeTrackingInterceptor(publisher, new ReplicationSequenceCounter()));
            var ctx = new TestDbContext(builder.Options);
            return ctx;
        }
    }

    private class TestType
    {
        public int Id { get; init; }
        public SubType SubType { get; init; } = new();
    }

    private class BlacklistedTestType
    {
        public int Id { get; init; }
        public SubType SubType { get; init; } = new();
    }

    private class SubType
    {
        public int Id { get; init; }
    }
}
