using AwesomeAssertions;
using Core.OS.Connections.Extensions;
using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.Instance;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Connections.Extensions;

public class IConnectionDbContextExtensionsTests : TestWithDbContextSqlite<ConnectionDbContextSqlite>
{
    private readonly ILocalInstanceInformationProvider _localInstanceInformationProvider = Substitute.For<ILocalInstanceInformationProvider>();

    private ServiceProvider CreateServiceProvider(Dictionary<string, string?>? settings = null)
    {
        var config = new TestConfig().AddCustomSettings(settings).BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton(_localInstanceInformationProvider);
        services.AddSingleton(config);
        services.AddSingleton<IConnectionDbContext>(_ => TestDbContext);
        services.AddMqttServices();

        return services.BuildServiceProvider();
    }

    public sealed class SeedConnections : IConnectionDbContextExtensionsTests
    {
        [Fact]
        public async Task Should_seed_system_default_tag()
        {
            // Arrange
            await using var serviceProvider = CreateServiceProvider();
            var token = TestContext.Current.CancellationToken;

            // Act
            await serviceProvider.SeedConnections(token);

            // Assert
            var dbContext = serviceProvider.GetRequiredService<IConnectionDbContext>();
            dbContext.Tags.FirstOrDefault(k => k.Id == ConnectionConstants.Tags.SystemDefault.Id).Should().NotBeNull();
        }

        [Fact]
        public async Task Should_seed_configured_mqtt_connections()
        {
            // Arrange
            await using var serviceProvider = CreateServiceProvider(CreateMqttClientConfig());
            var token = TestContext.Current.CancellationToken;

            // Act
            await serviceProvider.SeedConnections(token);

            // Assert
            var dbContext = serviceProvider.GetRequiredService<IConnectionDbContext>();
            dbContext.Connections.Count(k => k.Type == ConnectionType.Mqtt).Should().Be(2);
        }

        [Fact]
        public async Task Should_not_seed_disabled_mqtt_connections()
        {
            // Arrange
            await using var serviceProvider = CreateServiceProvider();
            var token = TestContext.Current.CancellationToken;

            // Act
            await serviceProvider.SeedConnections(token);

            // Assert
            var dbContext = serviceProvider.GetRequiredService<IConnectionDbContext>();
            dbContext.Connections.Where(k => k.Type == ConnectionType.Mqtt).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_remove_existing_mqtt_connections_when_disabled()
        {
            // Arrange
            await using var enabledProvider = CreateServiceProvider(CreateMqttClientConfig());
            await using var disabledProvider = CreateServiceProvider();
            var token = TestContext.Current.CancellationToken;

            // Act
            await enabledProvider.SeedConnections(token);
            await disabledProvider.SeedConnections(token);

            // Assert
            TestDbContext.Connections.Where(k => k.Type == ConnectionType.Mqtt).Should().BeEmpty();
        }
    }

    private static Dictionary<string, string?> CreateMqttClientConfig()
        => new()
        {
            { TestConfigKeys.MqttWebSocketClient.Endpoint, "wss://localhost/mqtt" },
            { TestConfigKeys.MqttWebSocketClient.Port, "4711" },
            { TestConfigKeys.MqttWebSocketClient.UserName, "user1" },
            { TestConfigKeys.MqttWebSocketClient.Password, "pwd1" },
            { TestConfigKeys.MqttWebSocketClient.TopicFilter, "data" },
            { TestConfigKeys.MqttServiceClient.Endpoint, "localhost" },
            { TestConfigKeys.MqttServiceClient.Port, "1234" },
            { TestConfigKeys.MqttServiceClient.UserName, "user2" },
            { TestConfigKeys.MqttServiceClient.Password, "pwd2" },
            { TestConfigKeys.MqttServiceClient.TopicFilter, "topic2" },
        };
}
