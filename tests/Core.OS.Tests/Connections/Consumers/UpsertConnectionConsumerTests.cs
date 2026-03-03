using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using Core.OS.Tests.Extensions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Connections;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class UpsertConnectionConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public static Tag TestTag { get; }
        = new("SystemDefault", new Guid("5CEBA391-0801-46F4-BB64-9935CAE15314")) { Protected = true };

    public UpsertConnectionConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<UpsertConnectionConsumer>();
            cfg.AddSingleton(Substitute.For<ILogger<UpsertConnectionConsumer>>());
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
        };


    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new UpsertConnection(new Connection());

        // Act + Assert
        await tester.TestCommand<UpsertConnection, UpsertConnectionConsumer>(command);
    }

    [Fact]
    public async Task Create_new_connection_should_publish_change_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var connection = ConnectionFactory.HttpConnection();
        var command = new UpsertConnection(connection);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        // Act
        await tester.TestCommand<UpsertConnection, UpsertConnectionConsumer>(command);

        // Assert
        await AssertUpsertConnectionConsumed(tester.Harness, dbContext, command, CrudAction.Created);
    }

    [Fact]
    public async Task Update_existing_element_should_publish_change_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        var connection = dbContext.SeedDatabaseConnection();
        var command = new UpsertConnection(connection);

        // Act
        await tester.TestCommand<UpsertConnection, UpsertConnectionConsumer>(command);

        // Assert
        await AssertUpsertConnectionConsumed(tester.Harness, dbContext, command, CrudAction.Updated);
    }

    [Fact]
    public async Task Create_connection_with_tags_should_use_existing_tags()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        dbContext.Tags.Add(ConnectionConstants.Tags.SystemDefault);
        await dbContext.SaveChangesAsync(tester.Harness.CancellationToken);

        var command = new UpsertConnection(ConnectionFactory.CreateSQLiteConnection(tags:
        [
            ConnectionConstants.Tags.SystemDefault,
        ]));

        // Act - delay needed to avoid context concurrency issues
        await tester.TestCommand<UpsertConnection, UpsertConnectionConsumer>(command);

        // Assert
        await AssertUpsertConnectionConsumed(tester.Harness, dbContext, command, CrudAction.Created);
    }

    [Fact]
    public async Task Update_existing_element_should_add_missing_tags()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        var existingConnection = dbContext.SeedDatabaseConnection(
        [
            ConnectionConstants.Tags.SystemDefault,
        ]);

        existingConnection.Tags.Add(TestTag);
        var command = new UpsertConnection(existingConnection);

        // Act - delay needed to avoid context concurrency issues
        await tester.TestCommand<UpsertConnection, UpsertConnectionConsumer>(command);

        // Assert
        await AssertUpsertConnectionConsumed(tester.Harness, dbContext, command, CrudAction.Updated);
        Assert.True(await tester.Harness.Published.Any<TagsChanged>(r =>
            r.Context.Message.Action == CrudAction.Created &&
            Equals(r.Context.Message.Tags[0], TestTag), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_existing_element_should_update_changed_tags()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        dbContext.Tags.Add(ConnectionConstants.Tags.SystemDefault);
        dbContext.Tags.Add(TestTag);
        var existingConnection = dbContext.SeedDatabaseConnection(
        [
            ConnectionConstants.Tags.SystemDefault,
        ]);

        var modifiedTag = new Tag("MODIFIED", TestTag.Id);
        existingConnection.Tags.Add(modifiedTag);
        var command = new UpsertConnection(existingConnection);

        // Act - delay needed to avoid context concurrency issues
        await tester.TestCommand<UpsertConnection, UpsertConnectionConsumer>(command);

        // Assert
        await AssertUpsertConnectionConsumed(tester.Harness, dbContext, command, CrudAction.Updated);
        Assert.True(await tester.Harness.Published.Any<TagsChanged>(r =>
            r.Context.Message.Action == CrudAction.Updated &&
            Equals(r.Context.Message.Tags[0], modifiedTag), TestContext.Current.CancellationToken));
    }

    private static async Task AssertUpsertConnectionConsumed(ITestHarness harness, IConnectionDbContext dbContext, UpsertConnection command, CrudAction action)
    {
        Assert.False(await harness.Published.Any<ConnectionErrorOccured>(TestContext.Current.CancellationToken));
        Assert.True(await harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == action &&
            Equals(r.Context.Message.Connection.Id, command.Connection.Id), TestContext.Current.CancellationToken));

        Assert.True(await dbContext.Connections.AnyAsync(c => c.Id == command.Connection.Id, TestContext.Current.CancellationToken));
    }
}
