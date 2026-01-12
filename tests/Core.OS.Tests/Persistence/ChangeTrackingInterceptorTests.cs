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

    public ChangeTrackingInterceptorTests()
    {
        _mockMediator = Substitute.For<ISuiteMediator>();
        _interceptor = new ChangeTrackingInterceptor(_mockMediator);
        _result = InterceptionResult<int>.SuppressWithResult(1);
        _cancellationToken = new CancellationToken();
    }

    [Fact]
    public async Task SavingChangesAsync_Should_Return_Result_If_Context_Is_Not_IModuleDbContext()
    {
        var context = Substitute.For<IModuleDbContext>();
        var eventData = new DbContextEventData(default!, default!, context.Instance);
        var returnedResult = await _interceptor.SavingChangesAsync(eventData, _result, _cancellationToken);

        // Verify the changes are not added to the queue
        Assert.Equal(returnedResult, _result);
    }

    [Fact]
    public async Task SavedChangesAsync_Should_Publish_DbChangeSet_If_Changes_Exist()
    {
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

        context.TestTypes.Add(entity);
        var result = await context.SaveChangesAsync(_cancellationToken);

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
    public async Task SavedChangesAsync_Should_Throw_DbUpdateException_If_Changes_Are_Not_Staged()
    {
        await using var context = TestDbContext.CreateContext(_mockMediator);
        var eventData = new SaveChangesCompletedEventData(default!, Do, context.Instance, 1);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _interceptor.SavedChangesAsync(eventData, 1, _cancellationToken).AsTask());
        Assert.Equal("Db was changed, but no changes were staged for distribution", exception.Message);

        static string Do(EventDefinitionBase def, EventData data)
            => string.Empty;
    }

    [Fact]
    public void SavingChanges_Should_Throw_InvalidOperationException()
    {
        var context = Substitute.For<IModuleDbContext>();
        var eventData = new DbContextEventData(default!, default!, context.Instance);
        var exception = Assert.Throws<InvalidOperationException>(() => _interceptor.SavingChanges(eventData, _result));
        Assert.Equal("Do not save changes synchronously. Use 'SaveChangesAsync' instead!", exception.Message);
    }

    [Fact]
    public async Task SavedChangesAsync_Should_Not_Publish_DbChangeSet_If_Changing_Blacklisted_Entity()
    {
        await using var context = TestDbContext.CreateContext(_mockMediator);

        var blacklistedEntity = new BlacklistedTestType
        {
            Id = 1,
            SubType = new SubType
            {
                Id = 1
            }
        };
        context.BlacklistedTestTypes.Add(blacklistedEntity);

        var entity = new TestType
        {
            Id = 1,
            SubType = new SubType
            {
                Id = 1
            }
        };
        context.TestTypes.Add(entity);

        var result = await context.SaveChangesAsync(_cancellationToken);

        var entityType = typeof(TestType);
        var expectedChange = new ChangedEntity(JsonSerializer.Serialize(entity, DefaultJsonSerializerSettings.Default),
            entityType.FullName ?? entityType.Name,
            entityType.Assembly.FullName,
            EntityState.Added);

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
    public async Task SavedChangesAsync_Should_Publish_DbChangeSet_If_Changing_Mixed_Entity()
    {
        await using var context = TestDbContext.CreateContext(_mockMediator);

        var entity = new BlacklistedTestType
        {
            Id = 1,
            SubType = new SubType
            {
                Id = 1
            }
        };

        context.BlacklistedTestTypes.Add(entity);
        var result = await context.SaveChangesAsync(_cancellationToken);

        _mockMediator.ReceivedCalls().Should().BeEmpty();

        Assert.Equal(2, result);
    }

    private interface ITestDbContext : IModuleDbContext
    {
        DbSet<TestType> TestTypes { get; }

        DbSet<BlacklistedTestType> BlacklistedTestTypes { get; }
    }

    private class TestDbContext(DbContextOptions<ChangeTrackingInterceptorTests.TestDbContext> dbContextOptions) :
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
                .AddInterceptors(new ChangeTrackingInterceptor(mediator));
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
