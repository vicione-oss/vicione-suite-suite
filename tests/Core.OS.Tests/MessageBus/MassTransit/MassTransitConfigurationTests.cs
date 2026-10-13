using Core.Module;
using Core.OS.MessageBus.MassTransit;
using Core.OS.MessageBus.MassTransit.Configuration;
using Core.OS.Modules;
using Core.OS.Tests.Extensions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.Testing.Backend;

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

    public sealed class AddMassTransitMessageBus : MassTransitConfigurationTests
    {
        [Fact]
        public async Task Should_register_services_for_standalone()
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
        public async Task Should_register_services_with_rabbit_mq()
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
        public async Task Should_add_local_bus_for_slave()
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
}
