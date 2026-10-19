using Blazor.Shared.UserManagement.ControlPanels.Role.Components;
using Blazor.Shared.UserManagement.ControlPanels.Role.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.Role.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.Role.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddRoleControlPanel
    {
        [Theory]
        [InlineData(typeof(RoleControlPanel))]
        [InlineData(typeof(RolePermissionsControlPanel))]
        public void Should_configure_a_reset_handler_for_each_tab(Type controlPanelType)
        {
            // Arrange
            var services = new ServiceCollection();
            var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType);

            // Act
            services.AddRoleControlPanel();

            // Assert
            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelResetHandler<RoleControlPanelState>));
        }

        [Theory]
        [InlineData(typeof(RoleControlPanel))]
        [InlineData(typeof(RolePermissionsControlPanel))]
        public void Should_configure_a_save_handler_for_each_tab(Type controlPanelType)
        {
            // Arrange
            var services = new ServiceCollection();
            var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType);

            // Act
            services.AddRoleControlPanel();

            // Assert
            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelSaveHandler<RoleControlPanelState>));
        }

        [Fact]
        public void Should_give_the_tab_the_state_of_the_role_control_panel()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRoleControlPanel();

            // Assert
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var parentState = scope.ServiceProvider.GetRequiredKeyedService<RoleControlPanelState>(typeof(ControlPanelServiceKey<RoleControlPanel>));
            var tabState = scope.ServiceProvider.GetRequiredKeyedService<RoleControlPanelState>(typeof(ControlPanelServiceKey<RolePermissionsControlPanel>));

            tabState.Should().BeSameAs(parentState);
        }

        [Theory]
        [InlineData(typeof(RoleControlPanelSaveHandler))]
        [InlineData(typeof(RoleControlPanelResetHandler))]
        public void Should_share_one_handler_owned_by_the_role_control_panel_between_all_tabs(Type handlerType)
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddRoleControlPanel();

            // Assert
            services.Should().ContainSingle(d => d.ServiceType == handlerType)
                .Which.ServiceKey.Should().Be(typeof(ControlPanelServiceKey<RoleControlPanel>));
        }
    }
}
