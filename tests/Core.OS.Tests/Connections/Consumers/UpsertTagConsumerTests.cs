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
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class UpsertTagConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public UpsertTagConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<UpsertTagConsumer>();
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
            cfg.AddSingleton(Substitute.For<ILogger<UpsertTagConsumer>>());
        };

    [Fact]
    public async Task Should_add_new_tag_and_publish_change_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var tag = new Tag("test", Guid.NewGuid());

        // Act
        var changeEvent = await tester.TestCommand<UpsertTag, UpsertTagConsumer, TagsChanged>(new UpsertTag(tag));

        // Assert
        changeEvent.Action.Should().Be(CrudAction.Created);
        changeEvent.Tags[0].Text.Should().Be(tag.Text);
    }

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        // Act + Assert
        await tester.TestCommand<UpsertTag, UpsertTagConsumer>(new UpsertTag(new Tag()));
    }

    [Fact]
    public async Task Should_update_existing_tag_and_publish_change_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var tagGuid = Guid.NewGuid();
        var commands = new UpsertTag[]
        {
            new(new Tag("Creation", tagGuid)),
            new(new Tag("Updated", tagGuid))
        };

        // Act
        var events = await tester.TestCommands<UpsertTag, UpsertTagConsumer, TagsChanged>(commands);

        // Assert
        events.Should().HaveCount(2);
        events.First().Action.Should().Be(CrudAction.Created);
        events.First().Tags[0].Text.Should().Be("Creation");

        events.Last().Action.Should().Be(CrudAction.Updated);
        events.Last().Tags[0].Text.Should().Be("Updated");
    }

    [Fact]
    public async Task Should_only_update_tag_with_matching_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        var tagAGuid = Guid.NewGuid();
        var tagBGuid = Guid.NewGuid();

        dbContext.Tags.Add(new Tag("TagA", tagAGuid));
        dbContext.Tags.Add(new Tag("TagB", tagBGuid));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var commands = new UpsertTag[]
        {
            new(new Tag("Updated", tagBGuid))
        };

        // Act
        var events = await tester.TestCommands<UpsertTag, UpsertTagConsumer, TagsChanged>(commands);

        // Assert
        events.Should().HaveCount(1);
        events.Last().Action.Should().Be(CrudAction.Updated);
        events.Last().Tags[0].Text.Should().Be("Updated");

        dbContext.Tags.FirstOrDefault(t => t.Id == tagAGuid)?.Text.Should().Be("TagA");
    }
}
