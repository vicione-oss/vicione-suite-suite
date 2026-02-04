using Core.Module;
using Core.OS.MessageBus.MassTransit;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Tests.Extensions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.MessageBus.MassTransit;

public class MassTransitConfigurationTests
{
    private static ServiceProvider SetupServiceProvider(IConfiguration config)
    {
        var moduleManager = Substitute.For<IModuleHost>();
        var suiteContext = new SuiteDependencyContext(new ModuleDependencyContext(ModuleType.Backend, "jsonPath", true),
            null,
            []);
        moduleManager.GetContext().Returns(suiteContext);

        return new ServiceCollection()
                .AddLogging()
                .AddSingleton(moduleManager)
                .AddSingleton(config)
                .AddInstanceServicesMock()
                .AddMassTransitMessageBus(config, _ => { }, [])
                .BuildServiceProvider();
    }

    [Fact]
    public async Task Add_message_bus_standalone_should_register_services()
    {
        // Arrange
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Standalone);

        // Act
        await using var serviceProvider = SetupServiceProvider(config.BuildConfiguration());

        // Assert
        Assert.NotNull(serviceProvider.GetService<IOptions<MessageBusOptions>>());
        Assert.NotNull(serviceProvider.GetService<IOptions<RabbitMqTransportOptions>>());

        Assert.NotNull(serviceProvider.GetService<IEndpointNameFormatter>());
        Assert.NotNull(serviceProvider.GetService<IRoutingSlipBuilder>());
        Assert.NotNull(serviceProvider.GetService<IRoutingSlipBuilderFactory>());
        Assert.NotNull(serviceProvider.GetService<IBus>());
    }

    [Fact]
    public async Task Add_mass_transit_message_bus_with_rabbit_mq_should_register_services()
    {
        // Arrange
        var config = new TestConfig()
            .UseInMemoryBus(false)
            .UseInstanceType(InstanceType.Standalone);

        // Act
        await using var serviceProvider = SetupServiceProvider(config.BuildConfiguration());

        // Assert
        Assert.NotNull(serviceProvider.GetService<IOptions<MessageBusOptions>>());
        Assert.NotNull(serviceProvider.GetService<IOptions<RabbitMqTransportOptions>>());

        Assert.NotNull(serviceProvider.GetService<IEndpointNameFormatter>());
        Assert.NotNull(serviceProvider.GetService<IRoutingSlipBuilder>());
        Assert.NotNull(serviceProvider.GetService<IRoutingSlipBuilderFactory>());
        Assert.NotNull(serviceProvider.GetService<IBus>());
    }

    [Fact]
    public async Task Add_mass_transit_message_bus_slave_should_add_local_bus()
    {
        // Arrange
        var config = new TestConfig()
            .UseInMemoryBus()
            .UseInstanceType(InstanceType.Slave);

        // Act
        await using var serviceProvider = SetupServiceProvider(config.BuildConfiguration());

        // Assert
        Assert.NotNull(serviceProvider.GetService<ILocalBus>());
    }
}
