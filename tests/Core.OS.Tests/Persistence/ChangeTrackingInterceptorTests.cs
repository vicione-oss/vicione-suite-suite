using System.Text.Json;
using Core.OS.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Backend.Persistence;
using Sdk.Messaging;
using Xunit;

namespace Core.OS.Tests.Persistence;

public class ChangeTrackingInterceptorTests
{
    private readonly CancellationToken _cancellationToken;
    private readonly ChangeTrackingInterceptor _interceptor;
    private readonly ISuiteMediator _mockMediator;
    private readonly InterceptionResult<int> _result;
    private readonly ReplicationSequenceCounter _sequenceCounter;

    public ChangeTrackingInterceptorTests()
    {
        _mockMediator = Substitute.For<ISuiteMediator>();
        _sequenceCounter = new ReplicationSequenceCounter();
        _interceptor = new ChangeTrackingInterceptor(_mockMediator, _sequenceCounter);
        _result = InterceptionResult<int>.SuppressWithResult(1);
        _cancellationToken = CancellationToken.None;
    }

    [Fact]
    public async Task SavingChangesAsync_should_skip_non_module_db_context()
    {
        // Arrange
        var eventData = new DbContextEventData(default!, default!, null);

        // Act
        var returnedResult = await _interceptor.SavingChangesAsync(eventData, _result, _cancellationToken);

        // Assert
        Assert.Equal(returnedResult, _result);
    }

    [Fact]
    public async Task SavingChangesAsync_should_publish_added_entity()
    {
        // Arrange
        await using var context = TestDbContext.CreateContext(_mockMediator);
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
        _ = _mockMediator.Received()
            .Publish(
                Arg.Is<DbChangeSet>(m => m.Changes.Count == 1
                    && m.Changes.First().EntityTypeFullName == expectedChange.EntityTypeFullName
                    && m.Changes.First().AssemblyFullName == expectedChange.AssemblyFullName
                    && m.Changes.First().State == expectedChange.State
                    && m.Changes.First().Entity == expectedChange.Entity),
                _cancellationToken);
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task SavingChangesAsync_should_publish_before_commit()
    {
        // Arrange
        await using var context = TestDbContext.CreateContext(_mockMediator);

        var entity = new TestType
        {
            Id = 1,
            SubType = new SubType
            {
                Id = 1
            }
        };
        context.TestTypes.Add(entity);

        // Act
        var result = await context.SaveChangesAsync(_cancellationToken);

        // Assert — publish is called during SavingChangesAsync (before commit),
        // verifying the Bus Outbox can capture it atomically (ADR-003 Gap 1).
        _ = _mockMediator.Received(1)
            .Publish(Arg.Any<DbChangeSet>(), _cancellationToken);
        Assert.Equal(2, result);
    }

    [Fact]
    public void SavingChanges_should_throw_invalid_operation_exception()
    {
        // Arrange
        var eventData = new DbContextEventData(default!, default!, null);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _interceptor.SavingChanges(eventData, _result));
        Assert.Equal("Do not save changes synchronously. Use 'SaveChangesAsync' instead!", exception.Message);
    }

    [Fact]
    public async Task SavingChangesAsync_should_exclude_blacklisted_entity_from_changeset()
    {
        // Arrange
        await using var context = TestDbContext.CreateContext(_mockMediator);
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
        _ = _mockMediator.Received()
            .Publish(
                Arg.Is<DbChangeSet>(m => m.Changes.Count == 1
                    && m.Changes.First().EntityTypeFullName == expectedChange.EntityTypeFullName
                    && m.Changes.First().AssemblyFullName == expectedChange.AssemblyFullName
                    && m.Changes.First().State == expectedChange.State
                    && m.Changes.First().Entity == expectedChange.Entity),
                _cancellationToken);
        Assert.Equal(4, result);
    }

    [Fact]
    public async Task SavingChangesAsync_should_not_publish_when_only_blacklisted_entities()
    {
        // Arrange
        await using var context = TestDbContext.CreateContext(_mockMediator);
        var entity = new BlacklistedTestType
        {
            Id = 1,
            SubType = new SubType { Id = 1 }
        };

        // Act
        context.BlacklistedTestTypes.Add(entity);
        var result = await context.SaveChangesAsync(_cancellationToken);

        // Assert
        _mockMediator.ReceivedCalls().Should().BeEmpty();
        Assert.Equal(2, result);
    }

    [Fact]
    public async Task SavingChangesAsync_should_publish_modified_entity()
    {
        // Arrange
        await using var context = TestDbContext.CreateContext(_mockMediator);
        var entity = new TestType { Id = 1, SubType = new SubType { Id = 1 } };
        context.TestTypes.Add(entity);
        await context.SaveChangesAsync(_cancellationToken);
        _mockMediator.ClearReceivedCalls();

        // Act
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync(_cancellationToken);

        // Assert
        _ = _mockMediator.Received(1)
            .Publish(
                Arg.Is<DbChangeSet>(m => m.Changes.Count == 1
                    && m.Changes.First().State == EntityState.Modified),
                _cancellationToken);
    }

    [Fact]
    public async Task SavingChangesAsync_should_publish_deleted_entity()
    {
        // Arrange
        await using var context = TestDbContext.CreateContext(_mockMediator);
        var entity = new TestType { Id = 1, SubType = new SubType { Id = 1 } };
        context.TestTypes.Add(entity);
        await context.SaveChangesAsync(_cancellationToken);
        _mockMediator.ClearReceivedCalls();

        // Act
        context.TestTypes.Remove(entity);
        await context.SaveChangesAsync(_cancellationToken);

        // Assert
        _ = _mockMediator.Received(1)
            .Publish(
                Arg.Is<DbChangeSet>(m => m.Changes.Count == 1
                    && m.Changes.First().State == EntityState.Deleted),
                _cancellationToken);
    }

    [Fact]
    public async Task SavingChangesAsync_should_propagate_publish_failure()
    {
        // Arrange — mediator throws to simulate outbox/broker failure
        _mockMediator
            .Publish(Arg.Any<DbChangeSet>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Broker unreachable")));

        await using var context = TestDbContext.CreateContext(_mockMediator);
        var entity = new TestType { Id = 1, SubType = new SubType { Id = 1 } };
        context.TestTypes.Add(entity);

        // Act & Assert — exception propagates, preventing the save from completing
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync(_cancellationToken));
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

        public static TestDbContext CreateContext(ISuiteMediator mediator)
        {
            var builder = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new ChangeTrackingInterceptor(mediator, new ReplicationSequenceCounter()));
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
