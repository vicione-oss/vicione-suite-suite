using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Settings.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddControlPanelInfrastructure
    {
        [Fact]
        public void Should_register_required_services()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddControlPanelInfrastructure();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var defaultControlPanelGroupDescriptor = serviceProvider.GetService<IDefaultControlPanelGroupDescriptor>();
            var controlPanelRegistryFactory = serviceProvider.GetService<IControlPanelRegistryFactory>();
            var controlPanelPageRegistry = serviceProvider.GetService<IControlPanelPageRegistry>();
            var controlPanelEditRegistry = serviceProvider.GetService<IControlPanelEditRegistry>();

            // Assert
            defaultControlPanelGroupDescriptor.Should().NotBeNull();
            controlPanelRegistryFactory.Should().NotBeNull();
            controlPanelPageRegistry.Should().NotBeNull();
            controlPanelEditRegistry.Should().NotBeNull();
        }
    }
}
