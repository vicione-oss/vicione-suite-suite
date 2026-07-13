using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using Core.OS.Tests.Extensions;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Requests;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class GetConnectionsConsumerTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public GetConnectionsConsumerTests()
    {
        _configureServices = cfg =>
        {
            cfg.AddConsumer<GetConnectionsConsumer>();
            cfg.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
        };
    }

    [Fact]
    public async Task Should_return_empty_list_for_unknown_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetConnections(Guid.NewGuid(), null);

        // Act
        var response = await tester.TestRequest<GetConnectionsResponse, GetConnections>(request);

        // Assert
        response.Connections.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_return_matching_connection_for_known_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var connection = tester.Services.GetRequiredService<IConnectionDbContext>().SeedDatabaseConnection();
        var request = new GetConnections(connection.Id, null);

        // Act
        var response = await tester.TestRequest<GetConnectionsResponse, GetConnections>(request);

        // Assert
        response.Connections.Should().ContainSingle();
        response.Connections.First().IsEqualTo(connection).Should().BeTrue();
    }

    [Fact]
    public async Task Should_return_only_matching_connections_when_filtered_by_type()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetConnections(null, [ConnectionType.Mqtt]);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        dbContext.SeedDatabaseConnection();
        var mqttConnection = dbContext.SeedMqttConnection();
        dbContext.SeedCloudConnection();

        // Act
        var response = await tester.TestRequest<GetConnectionsResponse, GetConnections>(request);

        // Assert
        response.Connections.Should().ContainSingle();
        response.Connections.First().IsEqualTo(mqttConnection).Should().BeTrue();
    }

    [Fact]
    public async Task Should_return_only_matching_connections_when_filtered_by_tag()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var tag1 = new Tag("Tag1");
        var tag2 = new Tag("Tag2");
        var tag3 = new Tag("Tag3");

        var request = new GetConnections(null, null, [tag1, tag2]);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();

        var expected = new List<Guid>
        {
            dbContext.SeedDatabaseConnection([tag1, tag2]).Id
        };
        dbContext.SeedDatabaseConnection([tag1, tag3]);
        dbContext.SeedCloudConnection();
        dbContext.SeedCloudConnection([tag1]);
        dbContext.SeedMqttConnection([tag1]);
        expected.Add(dbContext.SeedMqttConnection([tag1, tag2, tag3]).Id);
        dbContext.SeedMqttConnection([tag3]);

        // Act
        var response = await tester.TestRequest<GetConnectionsResponse, GetConnections>(request);

        // Assert
        response.Connections.Select(c => c.Id).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_return_only_matching_connections_when_filtered_by_instance_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var instanceId = Guid.NewGuid();
        var request = new GetConnections(null, null, null, instanceId);
        var dbContext = tester.Services.GetRequiredService<IConnectionDbContext>();
        var tag1 = new Tag("Tag1");
        var tag3 = new Tag("Tag3");
        dbContext.SeedDatabaseConnection([tag1, tag3]);
        dbContext.SeedCloudConnection();

        var expected = new List<Guid>
        {
            dbContext.SeedMqttConnection(instanceId, MqttConnectionType.WebSocket).Id,
            dbContext.SeedMqttConnection(instanceId, MqttConnectionType.TCP).Id
        };

        dbContext.SeedMqttConnection([tag3]);

        // Act
        var response = await tester.TestRequest<GetConnectionsResponse, GetConnections>(request);

        // Assert
        response.Connections.Select(c => c.Id).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_return_error_response_on_exception()
    {
        // Arrange
        var dbContextMock = Substitute.For<IConnectionDbContext>();
        dbContextMock.Connections.Throws(new ArgumentException("test"));

        var configureServices = _configureServices + (cfg => cfg.AddSingleton(dbContextMock));

        await using var tester = new MassTransitTester(configureServices);
        var instanceId = Guid.NewGuid();
        var request = new GetConnections(null, null, null, instanceId);

        // Act
        var response = await tester.TestRequest<GetConnectionsResponse, GetConnections>(request);

        // Assert
        response.Should().BeEquivalentTo(new GetConnectionsResponse([], new(ConnectionErrorCodes.UnknownError, "test")));
    }
}
