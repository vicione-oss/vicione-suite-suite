using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using Core.OS.Tests.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Connections.Commands;
using Sdk.Connections.Events;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class DeleteConnectionConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public DeleteConnectionConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<DeleteConnectionConsumer>();
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
        };
    }

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteConnection(Guid.NewGuid());

        // Act + Assert
        await tester.TestCommand<DeleteConnection, DeleteConnectionConsumer>(command);
    }

    [Fact]
    public async Task Remove_existing_connection_should_publish_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        var connection = dbContext.SeedDatabaseConnection();
        var command = new DeleteConnection(connection.Id);

        // Act
        await tester.TestCommand<DeleteConnection, DeleteConnectionConsumer>(command);

        // Assert
        Assert.False(await tester.Harness.Published.Any<ConnectionErrorOccured>(TestContext.Current.CancellationToken));
        Assert.True(await tester.Harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == CrudAction.Deleted &&
            r.Context.Message.Connection.Id == connection.Id, TestContext.Current.CancellationToken));

        Assert.False(await dbContext.Connections.AnyAsync(c => c.Id == connection.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Remove_unknown_connection_should_not_publish_change_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteConnection(Guid.NewGuid());

        // Act
        await tester.TestCommand<DeleteConnection, DeleteConnectionConsumer>(command);

        // Assert
        Assert.False(await tester.Harness.Published.Any<ConnectionChanged>(TestContext.Current.CancellationToken));
    }
}
