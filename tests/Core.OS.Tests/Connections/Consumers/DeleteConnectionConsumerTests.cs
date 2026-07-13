using AwesomeAssertions;
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
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DeleteConnectionConsumer>();
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteConnection(Guid.NewGuid());

        // Act + Assert
        await tester.TestCommand<DeleteConnection, DeleteConnectionConsumer>(command);
    }

    [Fact]
    public async Task Should_publish_changed_event_when_removing_existing_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        var connection = dbContext.SeedDatabaseConnection();
        var command = new DeleteConnection(connection.Id);

        // Act
        await tester.TestCommand<DeleteConnection, DeleteConnectionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == CrudAction.Deleted &&
            r.Context.Message.ErrorInfo == null &&
            r.Context.Message.Connection.Id == connection.Id, TestContext.Current.CancellationToken)).Should().BeTrue();

        (await dbContext.Connections.AnyAsync(c => c.Id == connection.Id, cancellationToken: TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_publish_success_event_for_unknown_connection_to_ensure_idempotency()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteConnection(Guid.NewGuid());

        // Act
        await tester.TestCommand<DeleteConnection, DeleteConnectionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == CrudAction.Deleted &&
            r.Context.Message.ErrorInfo == null &&
            r.Context.Message.Connection.Id == command.ConnectionId, TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
