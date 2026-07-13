using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Blazor.Shared.Tests.Models;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using Xunit;

namespace Blazor.Shared.Tests.Settings;

public sealed class SettingsModuleServiceTests
{
    private static readonly ILogger<SettingsModuleService> _logger = Substitute.For<ILogger<SettingsModuleService>>();
    private sealed class TestControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
    {
        public string Title => "Category1";
        public string? IconCssClass => "icon";
        public Uri? IconUrl => null;
        public int? Position => null;
    }

    private sealed class FooControlPanelDescriptor : IControlPanelDescriptor<FooControlPanel>
    {
        public string Title => "SubCategory9";
        public Uri IconUrl => new("icon.svg", UriKind.Relative);
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    private sealed class FooControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class BarControlPanelDescriptor : IControlPanelDescriptor<BarControlPanel>
    {
        public string Title => "SubCategory14";
        public Uri IconUrl => new("icon.svg", UriKind.Relative);
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    private class BarControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private static ServiceProvider SetupServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, FooControlPanel, ControlPanelState>()
            .WithAutoDiscovery<FooControlPanelDescriptor>();

        services.AddControlPanel<TestClientModuleB, BarControlPanel, ControlPanelState>()
            .WithAutoDiscovery<BarControlPanelDescriptor>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_get_categories_grouped_by_panel_elements()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistries = serviceProvider.GetRequiredService<IEnumerable<IControlPanelRegistry>>();
        var controlPanelRegistryItems = controlPanelRegistries.SelectMany(i => i);
        var defaultControlPanelGroupDescriptor = serviceProvider.GetRequiredService<IDefaultControlPanelGroupDescriptor>();

        var service = new SettingsModuleService(_logger);

        // Act
        var categories = service.GetSettingsCategoryMap(controlPanelRegistryItems, defaultControlPanelGroupDescriptor.Position);

        // Assert
        categories.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_get_sub_categories_grouped_by_panel_elements()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistries = serviceProvider.GetRequiredService<IEnumerable<IControlPanelRegistry>>();
        var controlPanelRegistryItems = controlPanelRegistries.SelectMany(i => i);
        var defaultControlPanelGroupDescriptor = serviceProvider.GetRequiredService<IDefaultControlPanelGroupDescriptor>();

        var service = new SettingsModuleService(_logger);
        var category = service.GetSettingsCategoryMap(controlPanelRegistryItems, defaultControlPanelGroupDescriptor.Position).Values.First();

        // Act
        var settingsEntries = service.GetSettingsEntries(controlPanelRegistryItems, groupPosition: 0, category.Title).ToList();

        // Assert
        settingsEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task Should_get_control_panels_by_category_and_sub_category()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProvider();
        var controlPanelRegistries = serviceProvider.GetRequiredService<IEnumerable<IControlPanelRegistry>>();
        var controlPanelRegistryItems = controlPanelRegistries.SelectMany(i => i);
        var defaultControlPanelGroupDescriptor = serviceProvider.GetRequiredService<IDefaultControlPanelGroupDescriptor>();

        var service = new SettingsModuleService(_logger);
        var categories = service.GetSettingsCategoryMap(controlPanelRegistryItems, defaultControlPanelGroupDescriptor.Position).Values;

        // Act + Assert
        foreach (var category in categories)
        {
            foreach (var settingsEntries in service.GetSettingsEntries(controlPanelRegistryItems, groupPosition: 0, category.Title))
            {
                settingsEntries.ControlPanelRegistryItem.Should().NotBeNull();
            }
        }
    }
}
