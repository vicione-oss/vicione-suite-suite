using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class DeleteTagConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
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
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        // Act + Assert
        await tester.TestCommand<DeleteTag, DeleteTagConsumer>(new DeleteTag(Guid.NewGuid()));
    }

    [Fact]
    public async Task Should_publish_success_event_when_delete_is_redelivered()
    {
        // Arrange — ADR-002: the tag has to exist and be deleted first, so the second delivery is a genuine
        // redelivery of an already-applied command rather than a delete of an unknown id.
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        var tagId = Guid.NewGuid();
        dbContext.Tags.Add(new Tag("test", tagId));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new DeleteTag(tagId);

        // Act — TestCommands waits for the bus to go idle before collecting, so both deliveries are complete
        var events = await tester.TestCommands<DeleteTag, DeleteTagConsumer, TagsChanged>([command, command]);

        // Assert
        dbContext.Tags.Should().BeEmpty("the terminal state has to be the same after the redelivery");

        events.Should().HaveCount(2, "the delete and its redelivery each publish a completion");
        events.Should().AllSatisfy(e =>
        {
            e.Action.Should().Be(CrudAction.Deleted);
            e.Tags[0].Id.Should().Be(tagId);
            e.ErrorInfo.Should().BeNull("a redelivered delete must not be reported as a failure");
        });
    }

    [Fact]
    public async Task Should_not_remove_protected_tag()
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
        Assert.True(await tester.Harness.Published.Any<TagsChanged>(r =>
            r.Context.Message.ErrorInfo != null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_remove_protected_tag_when_delete_if_protected_flag_is_set()
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
    public async Task Should_remove_tag()
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
