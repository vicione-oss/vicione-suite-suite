using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Blazor.Components.TabStrip.Components;
using static Blazor.Shared.Tests.Settings.Components.ControlPanelCarouselTests.TestClientModule;

namespace Blazor.Shared.Tests.Settings.Components;

public sealed class SettingsContainerContentHeaderTests
{
    private static BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddSettings();

            setup.Services.AddControlPanel<ControlPanelCarouselTests.TestClientModule, ParentWithChildControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ParentWithChildControlPanelDescriptor>();

            setup.Services.AddControlPanel<ControlPanelCarouselTests.TestClientModule, ChildOfParentControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ChildOfParentControlPanelDescriptor>();

            setup.Services.AddControlPanel<ControlPanelCarouselTests.TestClientModule, SecondParentWithChildControlPanel, ControlPanelState>()
                .WithAutoDiscovery<SecondParentWithChildControlPanelDescriptor>();

            setup.Services.AddControlPanel<ControlPanelCarouselTests.TestClientModule, ChildOfSecondParentControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ChildOfSecondParentControlPanelDescriptor>();

            setup.Services.AddControlPanel<ControlPanelCarouselTests.TestClientModule, ParentWithoutContentControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ParentWithoutContentControlPanelDescriptor>();

            setup.Services.AddControlPanel<ControlPanelCarouselTests.TestClientModule, ChildOfParentWithoutContentControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ChildOfParentWithoutContentControlPanelDescriptor>();

            setup.Services.AddControlPanel<ControlPanelCarouselTests.TestClientModule, FirstLevelControlPanel, ControlPanelState>()
                .WithAutoDiscovery<FirstLevelControlPanelDescriptor>();
        });

        return ctx;
    }

    private static IControlPanelRegistryItem ActivateControlPanel<TControlPanel>(BunitContext ctx)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<ControlPanelCarouselTests.TestClientModule>>();
        var controlPanelRegistryItem = controlPanelRegistry.First(i => i.ComponentType == typeof(TControlPanel));

        ctx.Services.GetRequiredService<SettingsModuleState>().ActiveControlPanelRegistryItem = controlPanelRegistryItem;

        return controlPanelRegistryItem;
    }

    /// <summary>
    /// Renders the pages the way a control panel does inside <see cref="ControlPanelContainer"/>; each page's content is its title.
    /// </summary>
    private static IRenderedComponent<CascadingValue<IControlPanelRegistryItem>> RenderControlPanelPages(BunitContext ctx,
        IControlPanelRegistryItem controlPanelRegistryItem, params string[] titles)
        => ctx.Render<CascadingValue<IControlPanelRegistryItem>>(parameters =>
        {
            parameters.Add(c => c.Value, controlPanelRegistryItem);

            foreach (var title in titles)
            {
                parameters.AddChildContent<ControlPanelPage>(page => page
                    .Add(p => p.Title, title)
                    .AddChildContent($"<p class=\"page-content\">{title}</p>"));
            }
        });

    private static List<string?> RenderTabTexts<TParent>(BunitContext ctx)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<ControlPanelCarouselTests.TestClientModule>>();
        var settingsModuleState = ctx.Services.GetRequiredService<SettingsModuleState>();
        settingsModuleState.ActiveControlPanelRegistryItem = controlPanelRegistry.First(i => i.ComponentType == typeof(TParent));

        var component = ctx.Render<SettingsContainerContentHeader>();

        return component.FindComponents<Tab>().Select(t => t.Instance.Text).ToList();
    }

    [Fact]
    public async Task Should_render_own_tab_before_children_for_parent_with_content()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var tabTexts = RenderTabTexts<ParentWithChildControlPanel>(ctx);

        // Assert
        tabTexts.Should().Equal("Parent", "Child");
    }

    [Fact]
    public async Task Should_activate_first_tab_when_switching_to_another_parent_with_the_same_tabs()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<ControlPanelCarouselTests.TestClientModule>>();
        var settingsModuleState = ctx.Services.GetRequiredService<SettingsModuleState>();

        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithChildControlPanel));
        var childItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ChildOfParentControlPanel));
        var otherParentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(SecondParentWithChildControlPanel));

        settingsModuleState.ActiveControlPanelRegistryItem = parentItem;
        settingsModuleState.SetActiveChild(parentItem, childItem);

        var component = ctx.Render<SettingsContainerContentHeader>();

        component.WaitForAssertion(() => component.Find(".tab.active").TextContent.Should().Be("Child"));

        // Act
        settingsModuleState.SetActiveControlPanel(otherParentItem);

        // Assert
        component.WaitForAssertion(() => component.Find(".tab.active").TextContent.Should().Be("Parent"));
    }

    [Fact]
    public async Task Should_render_only_children_as_tabs_for_parent_without_content()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var tabTexts = RenderTabTexts<ParentWithoutContentControlPanel>(ctx);

        // Assert
        tabTexts.Should().Equal("General");
    }

    [Fact]
    public async Task Should_render_control_panel_pages_as_tabs_for_control_panel_without_children()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        var controlPanelRegistryItem = ActivateControlPanel<FirstLevelControlPanel>(ctx);
        var component = ctx.Render<SettingsContainerContentHeader>();

        // Act
        var pages = RenderControlPanelPages(ctx, controlPanelRegistryItem, "Info", "Roles");

        // Assert
        component.WaitForAssertion(() => component.FindComponents<Tab>().Select(t => t.Instance.Text).Should().Equal("Info", "Roles"));
        component.Find(".tab.active").TextContent.Should().Be("Info");
        pages.Find(".page-content").TextContent.Should().Be("Info");
    }

    [Fact]
    public async Task Should_set_active_page_index_when_control_panel_page_tab_is_selected()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        var controlPanelRegistryItem = ActivateControlPanel<FirstLevelControlPanel>(ctx);
        var component = ctx.Render<SettingsContainerContentHeader>();

        var pages = RenderControlPanelPages(ctx, controlPanelRegistryItem, "Info", "Roles");

        // Act
        await component.WaitForElements(".tab")[1].ClickAsync();

        // Assert
        controlPanelRegistryItem.State.ActivePageIndex.Should().Be(1);
        component.WaitForAssertion(() => component.Find(".tab.active").TextContent.Should().Be("Roles"));

        // Stands in for ControlPanelContainer, which re-renders its control panel on every state change.
        foreach (var page in pages.FindComponents<ControlPanelPage>())
            page.Render();

        pages.Find(".page-content").TextContent.Should().Be("Roles");
    }

    [Fact]
    public async Task Should_render_page_title_instead_of_tabs_for_single_control_panel_page()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        var controlPanelRegistryItem = ActivateControlPanel<FirstLevelControlPanel>(ctx);
        var component = ctx.Render<SettingsContainerContentHeader>();

        // Act
        RenderControlPanelPages(ctx, controlPanelRegistryItem, "Info");

        // Assert
        component.WaitForAssertion(() => component.Find(".control-panel-title").TextContent.Trim().Should().Be("Info"));
        component.FindComponents<Tab>().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_render_children_instead_of_control_panel_pages_for_parent()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        var controlPanelRegistryItem = ActivateControlPanel<ParentWithChildControlPanel>(ctx);
        var component = ctx.Render<SettingsContainerContentHeader>();

        // Act
        RenderControlPanelPages(ctx, controlPanelRegistryItem, "Info", "Roles");

        // Assert
        component.FindComponents<Tab>().Select(t => t.Instance.Text).Should().Equal("Parent", "Child");
    }
}
