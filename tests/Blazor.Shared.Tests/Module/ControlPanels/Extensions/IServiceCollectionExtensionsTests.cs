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
    public static TheoryData<Action<IServiceCollection>, Type, Type> Tabs => new()
    {
        {
            s => s.AddModuleManagementInstalledControlPanel(),
            typeof(ModuleManagementInstalledControlPanel), typeof(ModuleManagementInstalledControlPanelState)
        },
        {
            s => s.AddModuleManagementBrowseControlPanel(),
            typeof(ModuleManagementBrowseControlPanel), typeof(ModuleManagementBrowseControlPanelState)
        },
    };

    public static TheoryData<Action<IServiceCollection>> ManagementServiceConsumers => new()
    {
        s => s.AddModuleManagementInstalledControlPanel(),
        s => s.AddModuleManagementBrowseControlPanel(),
        s => s.AddModuleDetailControlPanel(),
    };

    [Theory]
    [MemberData(nameof(Tabs))]
    public void Should_configure_save_and_reset_handler_for_each_tab(Action<IServiceCollection> addTab, Type controlPanelType,
        Type stateType)
    {
        // Arrange
        var services = new ServiceCollection();
        var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType);

        // Act
        addTab(services);

        // Assert - a tab without handlers of its own would neither load nor save
        services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
            && d.ServiceType == typeof(IControlPanelSaveHandler<>).MakeGenericType(stateType));

        services.Should().Contain(d => d.IsKeyedService && (Type?)d.ServiceKey == handlerServiceKey
            && d.ServiceType == typeof(IControlPanelResetHandler<>).MakeGenericType(stateType));
    }

    [Theory]
    [MemberData(nameof(Tabs))]
    public void Should_resolve_each_tab_state_with_its_dependencies(Action<IServiceCollection> addTab, Type controlPanelType,
        Type stateType)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddScoped(_ => Substitute.For<IModuleManagementService>());
        addTab(services);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        // Act
        var state = scope.ServiceProvider.GetRequiredKeyedService(stateType,
            typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelType));

        // Assert
        state.Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(ManagementServiceConsumers))]
    public void Should_register_the_management_service_for_each_control_panel_using_it(Action<IServiceCollection> addControlPanel)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        addControlPanel(services);

        // Assert
        services.Should().ContainSingle(d => d.ServiceType == typeof(IModuleManagementService))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Should_register_one_management_service_per_scope_for_all_control_panels()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddModuleManagementInstalledControlPanel()
            .AddModuleManagementBrowseControlPanel()
            .AddModuleDetailControlPanel();

        // Assert
        services.Should().ContainSingle(d => d.ServiceType == typeof(IModuleManagementService))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    public sealed class AddModuleManagementControlPanel
    {
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
    }
}
