using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using MassTransit;
using Microsoft.EntityFrameworkCore;
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
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new CreateConnection(new Connection());

        // Act + Assert
        await tester.TestCommand<CreateConnection, CreateConnectionConsumer>(command);
    }

    [Fact]
    public async Task Create_new_connection_should_publish_change_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var connection = ConnectionFactory.HttpConnection();
        var command = new CreateConnection(connection);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        // Act
        await tester.TestCommand<CreateConnection, CreateConnectionConsumer>(command);

        // Assert
        Assert.False(await tester.Harness.Published.Any<ConnectionErrorOccured>(TestContext.Current.CancellationToken));
        Assert.True(await tester.Harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == CrudAction.Created &&
            Equals(r.Context.Message.Connection.Id, command.Connection.Id), TestContext.Current.CancellationToken));

        Assert.True(await dbContext.Connections.AnyAsync(c => c.Id == command.Connection.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_existing_connection_should_not_publish_change_event()
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
        Assert.False(await tester.Harness.Published.Any<ConnectionErrorOccured>(TestContext.Current.CancellationToken));
        Assert.False(await tester.Harness.Published.Any<ConnectionChanged>(r =>
            r.Context.Message.Action == CrudAction.Created &&
            Equals(r.Context.Message.Connection.Id, command.Connection.Id), TestContext.Current.CancellationToken));

        Assert.True(await dbContext.Connections.AnyAsync(c => c.Id == command.Connection.Id, cancellationToken: TestContext.Current.CancellationToken));
    }
}
