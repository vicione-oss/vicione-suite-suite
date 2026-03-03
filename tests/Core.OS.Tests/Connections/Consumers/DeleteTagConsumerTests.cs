using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Connections.Consumers;

public class DeleteTagConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public DeleteTagConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DeleteTagConsumer>();
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
            cfg.AddSingleton(Substitute.For<ILogger<DeleteTagConsumer>>());
        };

    [Fact]
    public async Task Command_gets_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        // Act + Assert
        await tester.TestCommand<DeleteTag, DeleteTagConsumer>(new DeleteTag(Guid.NewGuid()));
    }

    [Fact]
    public async Task Does_not_remove_tag_if_protected()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        var tagId = Guid.NewGuid();
        var tag = new Tag("test", tagId)
        {
            Protected = true
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await tester.TestCommand<DeleteTag, DeleteTagConsumer>(new DeleteTag(tagId));

        // Assert
        dbContext.Tags.Should().Contain(tag);
        Assert.False(await tester.Harness.Published.Any<TagsChanged>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Removes_protected_tag_if_delete_if_protected_is_true()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        var tagId = Guid.NewGuid();
        var tag = new Tag("test", tagId)
        {
            Protected = true
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await tester.TestCommand<DeleteTag, DeleteTagConsumer>(new DeleteTag(tagId, true));

        // Assert
        dbContext.Tags.Should().HaveCount(0);
        Assert.True(await tester.Harness.Published.Any<TagsChanged>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Removes_tag()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        var tagId = Guid.NewGuid();
        var tag = new Tag("test", tagId);

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await tester.TestCommand<DeleteTag, DeleteTagConsumer>(new DeleteTag(tagId));

        // Assert
        dbContext.Tags.Should().HaveCount(0);
        Assert.True(await tester.Harness.Published.Any<TagsChanged>(TestContext.Current.CancellationToken));
    }
}
