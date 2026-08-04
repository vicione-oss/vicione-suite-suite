using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class CreateConnectionConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public static Tag TestTag { get; }
        = new("SystemDefault", new Guid("5CEBA391-0801-46F4-BB64-9935CAE15314")) { Protected = true };

    public CreateConnectionConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<CreateConnectionConsumer>();
            cfg.AddSingleton(Substitute.For<ILogger<CreateConnectionConsumer>>());
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
        };

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new CreateConnection(new Connection());

        // Act + Assert
        await tester.TestCommand<CreateConnection, CreateConnectionConsumer>(command);
    }

    [Fact]
    public async Task Should_publish_change_event_for_new_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var connection = ConnectionFactory.HttpConnection();
        var command = new CreateConnection(connection);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        // Act
        await tester.TestCommand<CreateConnection, CreateConnectionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == CrudAction.Created &&
            r.Context.Message.ErrorInfo == null &&
            Equals(r.Context.Message.Connection.Id, command.Connection.Id), TestContext.Current.CancellationToken)).Should().BeTrue();

        (await dbContext.Connections.AnyAsync(c => c.Id == command.Connection.Id, cancellationToken: TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_not_publish_change_event_for_existing_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var connection = ConnectionFactory.HttpConnection();
        var command = new CreateConnection(connection);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        dbContext.Connections.Add(connection);
        await ((ConnectionDbContext)dbContext).SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await tester.TestCommand<CreateConnection, CreateConnectionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == CrudAction.Created &&
            r.Context.Message.ErrorInfo == null &&
            Equals(r.Context.Message.Connection.Id, command.Connection.Id), TestContext.Current.CancellationToken)).Should().BeFalse();

        (await dbContext.Connections.AnyAsync(c => c.Id == command.Connection.Id, cancellationToken: TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
