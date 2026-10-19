using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Blazor.Shared.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;

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
        public Uri? IconUrl => new("icon.svg", UriKind.Relative);
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    private sealed class FooControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class BarControlPanelDescriptor : IControlPanelDescriptor<BarControlPanel>
    {
        public string Title => "SubCategory14";
        public Uri? IconUrl => new("icon.svg", UriKind.Relative);
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    private class BarControlPanel : ControlPanelBase<ControlPanelState>
    {
    }

    private sealed class GroupingParentDescriptor : IControlPanelParentDescriptor;

    private sealed class GroupingControlPanelDescriptor : IControlPanelDescriptor<GroupingControlPanel>
    {
        public string Title => "Grouping";
        public Uri? IconUrl => null;
        public bool HasContent => false;
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    [ControlPanelParent<GroupingParentDescriptor>]
    private sealed class GroupingControlPanel : ControlPanelBase<ControlPanelState>;

    private sealed class GroupedControlPanelDescriptor : IControlPanelDescriptor<GroupedControlPanel>
    {
        public string Title => "Grouped";
        public Uri? IconUrl => null;
    }

    [ControlPanelCategory<TestControlPanelCategoryDescriptor>]
    [ControlPanelChild<GroupingParentDescriptor>]
    private sealed class GroupedControlPanel : ControlPanelBase<ControlPanelState>;

    private static ServiceProvider SetupServiceProviderWithControlPanelWithoutContent(bool withChild)
    {
        var services = new ServiceCollection();

        services.AddControlPanelInfrastructure();
        services.AddControlPanel<TestClientModuleA, GroupingControlPanel, ControlPanelState>()
            .WithAutoDiscovery<GroupingControlPanelDescriptor>();

        if (withChild)
        {
            services.AddControlPanel<TestClientModuleA, GroupedControlPanel, ControlPanelState>()
                .WithAutoDiscovery<GroupedControlPanelDescriptor>();
        }

        return services.BuildServiceProvider();
    }

    private static List<string> GetSettingsEntryTitles(ServiceProvider serviceProvider)
    {
        var controlPanelRegistryItems = serviceProvider.GetRequiredService<IEnumerable<IControlPanelRegistry>>().SelectMany(i => i);

        return new SettingsModuleService(_logger)
            .GetSettingsEntries(controlPanelRegistryItems, groupPosition: 0, "Category1")
            .Select(e => e.Title)
            .ToList();
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

    [Fact]
    public async Task Should_list_a_control_panel_without_content_when_it_has_children()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProviderWithControlPanelWithoutContent(withChild: true);

        // Act
        var titles = GetSettingsEntryTitles(serviceProvider);

        // Assert
        titles.Should().Equal("Grouping");
    }

    [Fact]
    public async Task Should_not_list_a_control_panel_without_content_when_it_has_no_children()
    {
        // Arrange
        await using var serviceProvider = SetupServiceProviderWithControlPanelWithoutContent(withChild: false);

        // Act
        var titles = GetSettingsEntryTitles(serviceProvider);

        // Assert
        titles.Should().BeEmpty();
    }
}
