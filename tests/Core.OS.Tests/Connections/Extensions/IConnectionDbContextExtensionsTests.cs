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
        Assert.NotNull(dbContext.Tags.FirstOrDefault(k => k.Id == ConnectionConstants.Tags.SystemDefault.Id));
    }

    [Fact]
    public async Task ConfiguredMqttConnectionsGetSeeded()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider(CreateMqttClientConfig());
        var token = TestContext.Current.CancellationToken;

        // Act
        await serviceProvider.SeedConnections(token);

        // Assert
        var dbContext = serviceProvider.GetRequiredService<IConnectionDbContext>();
        Assert.Equal(2, dbContext.Connections.Count(k => k.Type == ConnectionType.Mqtt));
    }

    [Fact]
    public async Task DisabledMqttConnectionsDontGetSeeded()
    {
        // Arrange
        await using var serviceProvider = CreateServiceProvider();
        var token = TestContext.Current.CancellationToken;

        // Act
        await serviceProvider.SeedConnections(token);

        // Assert
        var dbContext = serviceProvider.GetRequiredService<IConnectionDbContext>();
        Assert.Empty(dbContext.Connections.Where(k => k.Type == ConnectionType.Mqtt));
    }

    [Fact]
    public async Task ExistingMqttConnectionsGetRemovedIfDisabled()
    {
        // Arrange
        await using var enabledProvider = CreateServiceProvider(CreateMqttClientConfig());
        await using var disabledProvider = CreateServiceProvider();
        var token = TestContext.Current.CancellationToken;

        // Act
        await enabledProvider.SeedConnections(token);
        await disabledProvider.SeedConnections(token);

        // Assert
        Assert.Empty(TestDbContext.Connections.Where(k => k.Type == ConnectionType.Mqtt));
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
