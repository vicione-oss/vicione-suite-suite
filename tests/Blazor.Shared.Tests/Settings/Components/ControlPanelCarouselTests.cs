using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using Sdk.Testing.Client;
using Sdk.Modules;
using static Blazor.Shared.Tests.Settings.Components.ControlPanelCarouselTests.TestClientModule;

namespace Blazor.Shared.Tests.Settings.Components;

public sealed class ControlPanelCarouselTests
{
    [Fact]
    public async Task Should_not_replace_current_control_panel_on_control_panel_request()
    {
        // Arrange
        FirstLevelControlPanel.ResetInitializationCount();

        await using var testContext = SetupTestContext(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();

        SetActiveControlPanel<FirstLevelControlPanel>(testContext);

        // Act
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Assert
        FirstLevelControlPanel.InitializationCount.Should().Be(1);

        component.FindComponents<SecondLevelControlPanel>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_not_replace_current_control_panel_on_navigate_back()
    {
        // Arrange
        SecondLevelControlPanel.ResetInitializationCount();

        await using var testContext = SetupTestContext(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        SetActiveControlPanel<SecondLevelControlPanel>(testContext);

        var navigateBackRequest = testContext.Services.GetRequiredService<INavigateBackRequest>();

        // Act
        await navigateBackRequest.Send();

        // Assert
        SecondLevelControlPanel.InitializationCount.Should().Be(1);

        component.FindComponents<FirstLevelControlPanel>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_navigate_back_when_requested_before_active_panel_is_set()
    {
        // Arrange
        // Two panels are on the requested stack but none is active.
        await using var testContext = SetupTestContext(out var transitionEnd);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();

        var firstLevel = controlPanelRegistry.First(i => i.ComponentType == typeof(FirstLevelControlPanel));
        var secondLevel = controlPanelRegistry.First(i => i.ComponentType == typeof(SecondLevelControlPanel));

        settingsModuleState.TryPushRequestedControlPanelRegistryItem(firstLevel);
        settingsModuleState.TryPushRequestedControlPanelRegistryItem(secondLevel);

        var navigateBackRequest = testContext.Services.GetRequiredService<INavigateBackRequest>();

        // Act
        await navigateBackRequest.Send();
        transitionEnd.SetResult(default!);

        // Assert
        component.WaitForAssertion(() =>
            settingsModuleState.RequestedControlPanelRegistryItems.Should().ContainSingle().Which.Should().Be(firstLevel));
    }

    [Fact]
    public async Task Should_keep_active_control_panel_until_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();

        // Act
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Assert
        settingsModuleState.ActiveControlPanelRegistryItem!.ComponentType.Should().Be<FirstLevelControlPanel>();
        component.FindComponents<FirstLevelControlPanel>().Should().ContainSingle();
        component.FindComponents<SecondLevelControlPanel>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_commit_requested_control_panel_when_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out var transitionEnd);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Act
        transitionEnd.SetResult(default!);

        // Assert
        component.WaitForAssertion(() =>
            settingsModuleState.ActiveControlPanelRegistryItem!.ComponentType.Should().Be<SecondLevelControlPanel>());
        settingsModuleState.ShowNavigateBackButton.Should().BeTrue();
    }

    [Fact]
    public async Task Should_keep_tabs_of_control_panel_sliding_out_until_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        testContext.Render<ControlPanelCarousel>();
        var header = testContext.Render<SettingsContainerContentHeader>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Act
        await testContext.Services.GetRequiredService<INavigateBackRequest>().Send();

        // Assert
        header.WaitForAssertion(() => header.FindAll(".tab-control-container").Should().ContainSingle());
    }

    [Fact]
    public async Task Should_show_title_of_target_control_panel_when_back_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out var transitionEnd);

        testContext.Render<ControlPanelCarousel>();
        var header = testContext.Render<SettingsContainerContentHeader>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        await testContext.Services.GetRequiredService<INavigateBackRequest>().Send();

        // Act
        transitionEnd.SetResult(default!);

        // Assert
        header.WaitForAssertion(() => header.Find(".control-panel-title").TextContent.Trim().Should().Be("First level"));
        header.FindAll(".tab-control-container").Should().BeEmpty();
    }

    [Fact]
    public async Task Should_unmount_leaving_control_panel_when_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out var transitionEnd);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Act
        transitionEnd.SetResult(default!);

        // Assert
        component.WaitForAssertion(() => component.FindComponents<FirstLevelControlPanel>().Should().BeEmpty());
        component.FindComponents<SecondLevelControlPanel>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_keep_control_panel_on_stack_until_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        var secondLevel = GetControlPanelRegistryItem<SecondLevelControlPanel>(testContext);
        secondLevel.State.ActivePageIndex = 1;

        var navigateBackRequest = testContext.Services.GetRequiredService<INavigateBackRequest>();

        // Act
        await navigateBackRequest.Send();

        // Assert
        settingsModuleState.RequestedControlPanelRegistryItems.Should().HaveCount(2)
            .And.EndWith(secondLevel);
        settingsModuleState.ActiveControlPanelRegistryItem.Should().Be(secondLevel);
        settingsModuleState.ShowNavigateBackButton.Should().BeTrue();
        component.FindComponents<SecondLevelControlPanel>().Should().ContainSingle();
        secondLevel.State.ActivePageIndex.Should().Be(1);
    }

    [Fact]
    public async Task Should_keep_page_content_of_control_panel_sliding_out()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Act
        await testContext.Services.GetRequiredService<INavigateBackRequest>().Send();

        // Assert
        component.FindAll(".second-level-page").Should().ContainSingle();
    }

    [Fact]
    public async Task Should_pop_control_panel_of_running_slide_on_second_navigate_back()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();
        await controlPanelRequest.Send<ThirdLevelControlPanel>();

        var navigateBackRequest = testContext.Services.GetRequiredService<INavigateBackRequest>();
        await navigateBackRequest.Send();

        // Act
        await navigateBackRequest.Send();

        // Assert
        settingsModuleState.RequestedControlPanelRegistryItems.Should().HaveCount(2);
        settingsModuleState.RequestedControlPanelRegistryItems[^1].ComponentType.Should().Be<SecondLevelControlPanel>();
        settingsModuleState.ActiveControlPanelRegistryItem!.ComponentType.Should().Be<SecondLevelControlPanel>();
    }

    [Fact]
    public async Task Should_unmount_popped_control_panel_and_reset_its_page_when_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out var transitionEnd);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        var secondLevel = GetControlPanelRegistryItem<SecondLevelControlPanel>(testContext);
        secondLevel.State.ActivePageIndex = 1;

        await testContext.Services.GetRequiredService<INavigateBackRequest>().Send();

        // Act
        transitionEnd.SetResult(default!);

        // Assert
        component.WaitForAssertion(() => component.FindComponents<SecondLevelControlPanel>().Should().BeEmpty());
        secondLevel.State.ActivePageIndex.Should().BeNull();
        settingsModuleState.RequestedControlPanelRegistryItems.Should().ContainSingle()
            .Which.ComponentType.Should().Be<FirstLevelControlPanel>();
    }

    [Fact]
    public async Task Should_end_slide_without_waiting_when_settings_navigation_replaces_control_panel()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        var firstLevel = settingsModuleState.RequestedControlPanelRegistryItems[0];

        // Act
        settingsModuleState.SetActiveControlPanel(firstLevel);

        // Assert
        component.WaitForAssertion(() => component.FindComponents<SecondLevelControlPanel>().Should().BeEmpty());
        var track = component.Find(".carousel__track");
        track.ClassList.Should().NotContain("sliding");
        track.GetAttribute("style").Should().Be("margin-left: 0%");
        component.FindComponents<FirstLevelControlPanel>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_reset_popped_control_panel_page_when_disposed_before_slide_ends()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        var secondLevel = GetControlPanelRegistryItem<SecondLevelControlPanel>(testContext);
        secondLevel.State.ActivePageIndex = 1;

        await testContext.Services.GetRequiredService<INavigateBackRequest>().Send();

        // Act
        // DisposeComponentsAsync skips the disposal while the renderer is busy (bUnit 2.11.3 clears its root components outside
        // the dispatcher), so the context is disposed instead.
        await testContext.DisposeAsync();

        // Assert
        secondLevel.State.ActivePageIndex.Should().BeNull();
    }

    [Fact]
    public async Task Should_reset_each_control_panel_once_with_its_own_handler_when_switching_tabs()
    {
        // Arrange
        SharedStateResetHandler.ResetCounter();
        ChildResetHandler.ResetCounter();

        await using var testContext = SetupTestContextWithParentAndChild(out var transitionEnd);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();

        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithChildControlPanel));
        var childItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ChildOfParentControlPanel));

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send(parentItem);

        SetActiveControlPanel<ParentWithChildControlPanel>(testContext);

        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        SharedStateResetHandler.ResetCount.Should().Be(1);
        ChildResetHandler.ResetCount.Should().Be(0);

        // Act - switch to the child, which shares the same IControlPanelState instance as the parent, back and again
        settingsModuleState.SetActiveChild(parentItem, childItem);
        component.WaitForAssertion(() => component.FindComponent<ChildOfParentControlPanel>().Should().NotBeNull());

        settingsModuleState.SetActiveChild(parentItem, null);
        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        settingsModuleState.SetActiveChild(parentItem, childItem);
        component.WaitForAssertion(() => component.FindComponent<ChildOfParentControlPanel>().Should().NotBeNull());

        // Assert - the child's first mount reset the child alone, the remounts reset nothing
        SharedStateResetHandler.ResetCount.Should().Be(1);
        ChildResetHandler.ResetCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_keep_running_edit_of_parent_beside_the_edit_of_child_when_switching_to_child()
    {
        // Arrange
        await using var testContext = SetupTestContextWithParentAndChild(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();
        var controlPanelEditRegistry = testContext.Services.GetRequiredService<IControlPanelEditRegistry>();

        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithChildControlPanel));
        var childItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ChildOfParentControlPanel));

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send(parentItem);

        SetActiveControlPanel<ParentWithChildControlPanel>(testContext);

        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        var runningEdit = controlPanelEditRegistry.Single();
        runningEdit.Begin();

        // Act
        settingsModuleState.SetActiveChild(parentItem, childItem);
        component.Render();

        // Assert
        component.WaitForAssertion(() => component.FindComponent<ChildOfParentControlPanel>().Should().NotBeNull());

        component.WaitForAssertion(() => controlPanelEditRegistry.Should().HaveCount(2));

        controlPanelEditRegistry.Should().Contain(runningEdit);
        runningEdit.IsRunning.Should().BeTrue();

        controlPanelEditRegistry.Single(e => e != runningEdit).IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Should_switch_control_panel_when_leaving_a_parent_from_a_child_tab()
    {
        // Arrange
        await using var testContext = SetupTestContextWithParentAndChild(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();

        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithChildControlPanel));
        var childItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ChildOfParentControlPanel));
        var otherTopLevelItem = controlPanelRegistry.First(i => i.ComponentType == typeof(SecondLevelControlPanel));

        settingsModuleState.SetActiveControlPanel(parentItem);
        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        settingsModuleState.SetActiveChild(parentItem, childItem);
        component.WaitForAssertion(() => component.FindComponent<ChildOfParentControlPanel>().Should().NotBeNull());

        // Act
        settingsModuleState.SetActiveControlPanel(otherTopLevelItem);

        // Assert
        component.WaitForAssertion(() => component.FindComponent<SecondLevelControlPanel>().Should().NotBeNull());
        component.FindComponents<ChildOfParentControlPanel>().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_switch_to_child_tab_after_returning_to_a_parent_left_with_a_reverted_edit()
    {
        // Arrange
        await using var testContext = SetupTestContextWithParentAndChild(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();
        var controlPanelEditRegistry = testContext.Services.GetRequiredService<IControlPanelEditRegistry>();

        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithChildControlPanel));
        var childItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ChildOfParentControlPanel));
        var otherTopLevelItem = controlPanelRegistry.First(i => i.ComponentType == typeof(SecondLevelControlPanel));

        settingsModuleState.SetActiveControlPanel(parentItem);
        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        var edit = controlPanelEditRegistry.Single();
        edit.Begin();

        settingsModuleState.SetActiveChild(parentItem, childItem);
        component.WaitForAssertion(() => component.FindComponent<ChildOfParentControlPanel>().Should().NotBeNull());

        await edit.Cancel(withReset: true);

        settingsModuleState.SetActiveControlPanel(otherTopLevelItem);
        component.WaitForAssertion(() => component.FindComponent<SecondLevelControlPanel>().Should().NotBeNull());

        settingsModuleState.SetActiveControlPanel(parentItem);
        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        // Act
        settingsModuleState.SetActiveChild(parentItem, childItem);

        // Assert
        component.WaitForAssertion(() => component.FindComponent<ChildOfParentControlPanel>().Should().NotBeNull());
    }

    [Fact]
    public async Task Should_remove_edits_when_top_level_control_panel_changes()
    {
        // Arrange
        await using var testContext = SetupTestContextWithParentAndChild(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();
        var controlPanelEditRegistry = testContext.Services.GetRequiredService<IControlPanelEditRegistry>();

        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithChildControlPanel));
        var otherTopLevelItem = controlPanelRegistry.First(i => i.ComponentType == typeof(SecondLevelControlPanel));

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send(parentItem);

        SetActiveControlPanel<ParentWithChildControlPanel>(testContext);

        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        var parentEdit = controlPanelEditRegistry.Single();

        // Act
        settingsModuleState.ClearRequestedControlPanelRegistryItems();
        settingsModuleState.TryPushRequestedControlPanelRegistryItem(otherTopLevelItem);

        SetActiveControlPanel<SecondLevelControlPanel>(testContext);

        // Assert
        component.WaitForAssertion(() => component.FindComponent<SecondLevelControlPanel>().Should().NotBeNull());

        controlPanelEditRegistry.Should().NotContain(parentEdit);
    }

    [Fact]
    public async Task Should_reset_state_again_when_navigating_back_to_a_control_panel()
    {
        // Arrange
        SharedStateResetHandler.ResetCounter();

        await using var testContext = SetupTestContextWithParentAndChild(out var transitionEnd);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();
        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithChildControlPanel));

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send(parentItem);

        SetActiveControlPanel<ParentWithChildControlPanel>(testContext);

        component.WaitForAssertion(() => component.FindComponent<ParentWithChildControlPanel>().Should().NotBeNull());

        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Ends this slide and, the result staying set, the one back as well.
        transitionEnd.SetResult(default!);

        component.WaitForAssertion(() => component.FindComponents<ParentWithChildControlPanel>().Should().BeEmpty());

        var resetCountBeforeNavigateBack = SharedStateResetHandler.ResetCount;

        var navigateBackRequest = testContext.Services.GetRequiredService<INavigateBackRequest>();

        // Act
        await navigateBackRequest.Send();

        // Assert
        component.WaitForAssertion(
            () => SharedStateResetHandler.ResetCount.Should().Be(resetCountBeforeNavigateBack + 1),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_render_first_child_instead_of_parent_without_content()
    {
        // Arrange
        await using var testContext = new BunitContext();
        testContext.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddSettings();

            setup.Services.AddControlPanel<TestClientModule, ParentWithoutContentControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ParentWithoutContentControlPanelDescriptor>();

            setup.Services.AddControlPanel<TestClientModule, ChildOfParentWithoutContentControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ChildOfParentWithoutContentControlPanelDescriptor>();
        });

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();
        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentWithoutContentControlPanel));

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();

        // Act
        await controlPanelRequest.Send(parentItem);

        SetActiveControlPanel<ParentWithoutContentControlPanel>(testContext);

        // Assert
        component.WaitForAssertion(() => component.FindComponent<ChildOfParentWithoutContentControlPanel>().Should().NotBeNull());

        component.FindComponents<ParentWithoutContentControlPanel>().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_load_the_entity_once_when_switching_to_a_tab_sharing_the_reset_handler()
    {
        // Arrange
        await using var testContext = SetupTestContextWithParentAndTabSharingTheResetHandler();

        var component = testContext.Render<ControlPanelCarousel>();

        var (parentItem, tabItem) = await ShowParentOfTabSharingTheResetHandler(testContext, component);

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var resetCounter = testContext.Services.GetRequiredService<ResetCounter>();

        component.WaitForAssertion(() => resetCounter.ResetCount.Should().Be(1));

        // Act
        settingsModuleState.SetActiveChild(parentItem, tabItem);
        component.Render();

        // Assert
        component.WaitForAssertion(() => component.FindComponent<TabSharingTheResetHandlerControlPanel>().Should().NotBeNull());

        resetCounter.ResetCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_reset_the_entity_when_cancelling_a_change_made_only_on_a_tab_sharing_the_reset_handler()
    {
        // Arrange
        await using var testContext = SetupTestContextWithParentAndTabSharingTheResetHandler();

        var component = testContext.Render<ControlPanelCarousel>();

        var (parentItem, tabItem) = await ShowParentOfTabSharingTheResetHandler(testContext, component);

        var settingsModuleState = testContext.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelEditRegistry = testContext.Services.GetRequiredService<IControlPanelEditRegistry>();
        var resetCounter = testContext.Services.GetRequiredService<ResetCounter>();

        var parentEdit = controlPanelEditRegistry.Single();

        settingsModuleState.SetActiveChild(parentItem, tabItem);
        component.Render();

        component.WaitForAssertion(() => controlPanelEditRegistry.Should().HaveCount(2));

        var tabEdit = controlPanelEditRegistry.Single(e => e != parentEdit);
        tabEdit.Begin();

        // Act
        await tabEdit.Cancel(withReset: true);

        // Assert
        resetCounter.ResetCount.Should().Be(2);
        tabEdit.IsRunning.Should().BeFalse();
    }

    private static async Task<(IControlPanelRegistryItem Parent, IControlPanelRegistryItem Tab)> ShowParentOfTabSharingTheResetHandler(
        BunitContext testContext, IRenderedComponent<ControlPanelCarousel> component)
    {
        var controlPanelRegistry = testContext.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();

        var parentItem = controlPanelRegistry.First(i => i.ComponentType == typeof(ParentSharingTheResetHandlerControlPanel));
        var tabItem = controlPanelRegistry.First(i => i.ComponentType == typeof(TabSharingTheResetHandlerControlPanel));

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send(parentItem);

        SetActiveControlPanel<ParentSharingTheResetHandlerControlPanel>(testContext);

        component.WaitForAssertion(() => component.FindComponent<ParentSharingTheResetHandlerControlPanel>().Should().NotBeNull());

        return (parentItem, tabItem);
    }

    private static BunitContext SetupTestContextWithParentAndTabSharingTheResetHandler()
    {
        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            SetupCarouselModule(setup, new TaskCompletionSource<IJSVoidResult>());

            setup.Services.AddSettings();

            setup.Services.AddSingleton<ResetCounter>();

            setup.Services.AddControlPanel<TestClientModule, ParentSharingTheResetHandlerControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ParentSharingTheResetHandlerControlPanelDescriptor>()
                .WithSharedHandlers(handlers => handlers.WithResetHandler<CountingResetHandler>(), out var sharedHandlers);

            setup.Services.AddControlPanel<TestClientModule, TabSharingTheResetHandlerControlPanel, ControlPanelState>()
                .WithAutoDiscovery<TabSharingTheResetHandlerControlPanelDescriptor>()
                .WithSharedHandlers(sharedHandlers);
        });

        return ctx;
    }

    private static BunitContext SetupTestContextWithParentAndChild(out TaskCompletionSource<IJSVoidResult> transitionEnd)
    {
        var slideEnd = new TaskCompletionSource<IJSVoidResult>();
        transitionEnd = slideEnd;

        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            SetupCarouselModule(setup, slideEnd);

            setup.Services.AddSettings();

            setup.Services.AddControlPanel<TestClientModule, ParentWithChildControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ParentWithChildControlPanelDescriptor>()
                .WithResetHandler<SharedStateResetHandler>();

            setup.Services.AddControlPanel<TestClientModule, ChildOfParentControlPanel, ControlPanelState>()
                .WithAutoDiscovery<ChildOfParentControlPanelDescriptor>()
                .WithResetHandler<ChildResetHandler>();

            // The child shares the parent's IControlPanelState instance instead of getting its own, as the tabs of a
            // network interface do. Only then do the tests tell an edit per control panel from an edit per state.
            setup.Services.AddKeyedScoped<ControlPanelState>(typeof(ControlPanelServiceKey<ChildOfParentControlPanel>),
                (sp, _) => sp.GetRequiredKeyedService<ControlPanelState>(typeof(ControlPanelServiceKey<ParentWithChildControlPanel>)));

            setup.Services.AddControlPanel<TestClientModule, SecondLevelControlPanel, ControlPanelState>()
                .WithAutoDiscovery<SecondLevelControlPanelDescriptor>();
        });

        return ctx;
    }

    private static void SetActiveControlPanel<TControlPanel>(BunitContext ctx)
        where TControlPanel : ControlPanelBase<ControlPanelState>
    {
        var settingsModuleState = ctx.Services.GetRequiredService<SettingsModuleState>();
        settingsModuleState.ActiveControlPanelRegistryItem = GetControlPanelRegistryItem<TControlPanel>(ctx);
    }

    private static IControlPanelRegistryItem GetControlPanelRegistryItem<TControlPanel>(BunitContext ctx)
        where TControlPanel : ControlPanelBase<ControlPanelState>
        => ctx.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>().First(i => i.ComponentType == typeof(TControlPanel));

    private static void SetupCarouselModule(ClientServiceConfigurator setup, TaskCompletionSource<IJSVoidResult> slideEnd)
    {
        var carouselModule = Substitute.For<IJSObjectReference>();
#pragma warning disable CA2012 // NSubstitute only records the call here; the returned ValueTask is never consumed.
        carouselModule.InvokeAsync<IJSVoidResult>("waitForTransitionEnd", Arg.Any<CancellationToken>(), Arg.Any<object?[]?>())
            .Returns(_ => new ValueTask<IJSVoidResult>(slideEnd.Task));
#pragma warning restore CA2012
        setup.JsInterop.IncludeModuleScript<SharedClientModule>("carousel.js", Arg.Any<CancellationToken>())
            .Returns(carouselModule);
    }

    private static BunitContext SetupTestContext(out TaskCompletionSource<IJSVoidResult> transitionEnd)
    {
        var slideEnd = new TaskCompletionSource<IJSVoidResult>();
        transitionEnd = slideEnd;

        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            SetupCarouselModule(setup, slideEnd);

            setup.Services.AddSettings();

            setup.Services.AddControlPanel<TestClientModule, FirstLevelControlPanel, ControlPanelState>()
                    .WithAutoDiscovery<FirstLevelControlPanelDescriptor>();

            setup.Services.AddControlPanel<TestClientModule, SecondLevelControlPanel, ControlPanelState>()
                    .WithAutoDiscovery<SecondLevelControlPanelDescriptor>();

            setup.Services.AddControlPanel<TestClientModule, ThirdLevelControlPanel, ControlPanelState>()
                    .WithAutoDiscovery<ThirdLevelControlPanelDescriptor>();
        });

        return ctx;
    }

    public sealed class TestClientModule : IClientModule
    {
        public ModuleKey ModuleKey => new();

        internal sealed class ControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
        {
            public string Title => "Test category";
        }

        public class TestControlPanel : ControlPanelBase<ControlPanelState>;

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        public sealed class FirstLevelControlPanel : TestControlPanel
        {
            private static int _initializationCount;

            public static int InitializationCount => _initializationCount;

            protected override void OnInitialized()
            {
                base.OnInitialized();

                _initializationCount++;
            }

            public static void ResetInitializationCount()
                => _initializationCount = 0;
        }

        public sealed class FirstLevelControlPanelDescriptor : IControlPanelDescriptor<FirstLevelControlPanel>
        {
            public string Title => "First level";
            public Uri? IconUrl => new("icon.svg", UriKind.Relative);
        }

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        public sealed class SecondLevelControlPanel : TestControlPanel
        {
            private static int _initializationCount;

            public static int InitializationCount => _initializationCount;

            protected override void OnInitialized()
            {
                base.OnInitialized();

                _initializationCount++;
            }

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                builder.OpenComponent<ControlPanelPage>(0);
                builder.AddComponentParameter(1, nameof(ControlPanelPage.Title), "Second level page");
                builder.AddComponentParameter(2, nameof(ControlPanelPage.ChildContent),
                    (RenderFragment)(content => content.AddMarkupContent(0, """<div class="second-level-page"></div>""")));
                builder.CloseComponent();

                builder.OpenComponent<ControlPanelPage>(3);
                builder.AddComponentParameter(4, nameof(ControlPanelPage.Title), "Second level second page");
                builder.AddComponentParameter(5, nameof(ControlPanelPage.ChildContent),
                    (RenderFragment)(content => content.AddMarkupContent(0, """<div class="second-level-second-page"></div>""")));
                builder.CloseComponent();
            }

            public static void ResetInitializationCount()
                => _initializationCount = 0;
        }

        public sealed class SecondLevelControlPanelDescriptor : IControlPanelDescriptor<SecondLevelControlPanel>
        {
            public string Title => "Second level";
            public Uri? IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        public sealed class ThirdLevelControlPanel : TestControlPanel;

        public sealed class ThirdLevelControlPanelDescriptor : IControlPanelDescriptor<ThirdLevelControlPanel>
        {
            public string Title => "Third level";
            public Uri? IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }

        internal sealed class SharedParentDescriptor : IControlPanelParentDescriptor;

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelParent<SharedParentDescriptor>]
        public sealed class ParentWithChildControlPanel : TestControlPanel;

        public sealed class ParentWithChildControlPanelDescriptor : IControlPanelDescriptor<ParentWithChildControlPanel>
        {
            public string Title => "Parent";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
        }

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelChild<SharedParentDescriptor>]
        public sealed class ChildOfParentControlPanel : TestControlPanel;

        public sealed class ChildOfParentControlPanelDescriptor : IControlPanelDescriptor<ChildOfParentControlPanel>
        {
            public string Title => "Child";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }

        internal sealed class SecondSharedParentDescriptor : IControlPanelParentDescriptor;

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelParent<SecondSharedParentDescriptor>]
        public sealed class SecondParentWithChildControlPanel : TestControlPanel;

        public sealed class SecondParentWithChildControlPanelDescriptor : IControlPanelDescriptor<SecondParentWithChildControlPanel>
        {
            public string Title => "Parent";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
        }

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelChild<SecondSharedParentDescriptor>]
        public sealed class ChildOfSecondParentControlPanel : TestControlPanel;

        public sealed class ChildOfSecondParentControlPanelDescriptor : IControlPanelDescriptor<ChildOfSecondParentControlPanel>
        {
            public string Title => "Child";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }

        internal sealed class ParentWithoutContentDescriptor : IControlPanelParentDescriptor;

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelParent<ParentWithoutContentDescriptor>]
        public sealed class ParentWithoutContentControlPanel : TestControlPanel;

        public sealed class ParentWithoutContentControlPanelDescriptor : IControlPanelDescriptor<ParentWithoutContentControlPanel>
        {
            public string Title => "Parent without content";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public bool HasContent => false;
        }

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelChild<ParentWithoutContentDescriptor>]
        public sealed class ChildOfParentWithoutContentControlPanel : TestControlPanel;

        public sealed class ChildOfParentWithoutContentControlPanelDescriptor : IControlPanelDescriptor<ChildOfParentWithoutContentControlPanel>
        {
            public string Title => "General";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }

        internal sealed class ParentSharingTheResetHandlerDescriptor : IControlPanelParentDescriptor;

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelParent<ParentSharingTheResetHandlerDescriptor>]
        public sealed class ParentSharingTheResetHandlerControlPanel : TestControlPanel;

        public sealed class ParentSharingTheResetHandlerControlPanelDescriptor : IControlPanelDescriptor<ParentSharingTheResetHandlerControlPanel>
        {
            public string Title => "Parent sharing the reset handler";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
        }

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        [ControlPanelChild<ParentSharingTheResetHandlerDescriptor>]
        public sealed class TabSharingTheResetHandlerControlPanel : TestControlPanel;

        public sealed class TabSharingTheResetHandlerControlPanelDescriptor : IControlPanelDescriptor<TabSharingTheResetHandlerControlPanel>
        {
            public string Title => "Tab sharing the reset handler";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }

        internal sealed class ResetCounter
        {
            public int ResetCount { get; private set; }

            public void Count() => ResetCount++;
        }

        internal sealed class CountingResetHandler(ResetCounter resetCounter) : IControlPanelResetHandler<ControlPanelState>
        {
            public Task Reset(ControlPanelState state, CancellationToken cancellationToken)
            {
                resetCounter.Count();

                return Task.CompletedTask;
            }
        }

        internal sealed class SharedStateResetHandler : IControlPanelResetHandler<ControlPanelState>
        {
            private static int _resetCount;

            public static int ResetCount => _resetCount;

            public static void ResetCounter() => _resetCount = 0;

            public Task Reset(ControlPanelState state, CancellationToken cancellationToken)
            {
                _resetCount++;

                return Task.CompletedTask;
            }
        }

        internal sealed class ChildResetHandler : IControlPanelResetHandler<ControlPanelState>
        {
            private static int _resetCount;

            public static int ResetCount => _resetCount;

            public static void ResetCounter() => _resetCount = 0;

            public Task Reset(ControlPanelState state, CancellationToken cancellationToken)
            {
                _resetCount++;

                return Task.CompletedTask;
            }
        }
    }
}
