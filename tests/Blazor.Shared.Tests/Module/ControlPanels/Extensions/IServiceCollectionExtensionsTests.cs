using Blazor.Shared.Module.ControlPanels;
using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Module.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.Module.ControlPanels.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddModuleManagementControlPanel
    {
        [Theory]
        [InlineData(typeof(ModuleManagementInstalledControlPanel), typeof(ModuleManagementInstalledControlPanelState))]
        [InlineData(typeof(ModuleManagementBrowseControlPanel), typeof(ModuleManagementBrowseControlPanelState))]
        public void Should_configure_save_and_reset_handler_for_each_tab(Type controlPanelType, Type stateType)
        {
            // Arrange
            var services = new ServiceCollection();
            var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType);

            // Act
            services.AddModuleManagementControlPanel();

            // Assert - a tab without handlers of its own would neither load nor save
            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelSaveHandler<>).MakeGenericType(stateType));

            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelResetHandler<>).MakeGenericType(stateType));
        }

        [Fact]
        public void Should_not_configure_handlers_for_the_parent()
        {
            // Arrange
            var services = new ServiceCollection();
            var handlerServiceKey = typeof(ControlPanelServiceKey<ModuleManagementControlPanel>);

            // Act
            services.AddModuleManagementControlPanel();

            // Assert
            services.Should().NotContain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType.IsGenericType
                && (d.ServiceType.GetGenericTypeDefinition() == typeof(IControlPanelSaveHandler<>)
                    || d.ServiceType.GetGenericTypeDefinition() == typeof(IControlPanelResetHandler<>)));
        }

        [Fact]
        public void Should_resolve_each_tab_state_with_its_dependencies()
        {
            // Arrange
            var services = new ServiceCollection()
                .AddScoped(_ => Substitute.For<IModuleManagementService>());
            services.AddModuleManagementControlPanel();

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            // Act
            var installedState = scope.ServiceProvider.GetRequiredKeyedService<ModuleManagementInstalledControlPanelState>(
                typeof(ControlPanelServiceKey<ModuleManagementInstalledControlPanel>));
            var browseState = scope.ServiceProvider.GetRequiredKeyedService<ModuleManagementBrowseControlPanelState>(
                typeof(ControlPanelServiceKey<ModuleManagementBrowseControlPanel>));

            // Assert
            installedState.Should().NotBeNull();
            browseState.Should().NotBeNull();
        }
    }
}
