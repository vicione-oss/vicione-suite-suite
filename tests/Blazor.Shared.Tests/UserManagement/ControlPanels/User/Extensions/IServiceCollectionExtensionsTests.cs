using Blazor.Shared.UserManagement.ControlPanels.User.Components;
using Blazor.Shared.UserManagement.ControlPanels.User.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.User.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.User.Extensions;

public class IServiceCollectionExtensionsTests
{
    public sealed class AddUserControlPanel
    {
        [Theory]
        [InlineData(typeof(UserControlPanel))]
        [InlineData(typeof(UserRolesControlPanel))]
        [InlineData(typeof(UserPermissionsControlPanel))]
        public void Should_configure_a_reset_handler_for_each_tab(Type controlPanelType)
        {
            // Arrange
            var services = new ServiceCollection();
            var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType);

            // Act
            services.AddUserControlPanel();

            // Assert
            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelResetHandler<UserControlPanelState>));
        }

        [Theory]
        [InlineData(typeof(UserControlPanel))]
        [InlineData(typeof(UserRolesControlPanel))]
        [InlineData(typeof(UserPermissionsControlPanel))]
        public void Should_configure_a_save_handler_for_each_tab(Type controlPanelType)
        {
            // Arrange
            var services = new ServiceCollection();
            var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType);

            // Act
            services.AddUserControlPanel();

            // Assert
            services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
                && d.ServiceType == typeof(IControlPanelSaveHandler<UserControlPanelState>));
        }

        [Theory]
        [InlineData(typeof(UserRolesControlPanel))]
        [InlineData(typeof(UserPermissionsControlPanel))]
        public void Should_give_each_tab_the_state_of_the_user_control_panel(Type tabType)
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddUserControlPanel();

            // Assert
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var parentState = scope.ServiceProvider.GetRequiredKeyedService<UserControlPanelState>(typeof(ControlPanelServiceKey<UserControlPanel>));
            var tabState = scope.ServiceProvider.GetRequiredKeyedService<UserControlPanelState>(typeof(ControlPanelServiceKey<>).MakeGenericType(tabType));

            tabState.Should().BeSameAs(parentState);
        }

        [Theory]
        [InlineData(typeof(UserControlPanelSaveHandler))]
        [InlineData(typeof(UserControlPanelResetHandler))]
        public void Should_share_one_handler_owned_by_the_user_control_panel_between_all_tabs(Type handlerType)
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddUserControlPanel();

            // Assert
            services.Should().ContainSingle(d => d.ServiceType == handlerType)
                .Which.ServiceKey.Should().Be(typeof(ControlPanelServiceKey<UserControlPanel>));
        }
    }
}
