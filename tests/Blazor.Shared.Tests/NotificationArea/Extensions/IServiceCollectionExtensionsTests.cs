using Blazor.Shared.NotificationArea.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Extensions;
using Sdk.Client.NotificationArea.Services;
using TestModule.Client;
using TestModule.Client.NotificationArea;

namespace Blazor.Shared.Tests.NotificationArea.Extensions;

public sealed class IServiceCollectionExtensionsTests
{
    public sealed class AddNotificationElements
    {
        [Fact]
        public void Should_register_registry_and_states_for_annotated_elements()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddNotificationElementInfrastructure();

            // Act
            services.AddNotificationElements<TestClientModule>();
            var provider = services.BuildServiceProvider();

            // Assert
            var registry = provider.GetService<INotificationElementRegistry<TestClientModule>>();
            registry.Should().NotBeNull();

            var allRegistries = provider.GetServices<INotificationElementRegistry>().ToList();
            allRegistries.Should().Contain(registry!);
        }

        [Fact]
        public void Should_instantiate_registry_with_initial_elements()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddNotificationElementInfrastructure();

            services.AddNotificationElements<TestClientModule>();
            var provider = services.BuildServiceProvider();

            // Act
            var registry = provider.GetRequiredService<INotificationElementRegistry<TestClientModule>>();
            var elements = registry.ToList();

            // Assert
            elements.Should().ContainSingle(e => e.ComponentType == typeof(TestNotificationElement));
        }
    }
}

