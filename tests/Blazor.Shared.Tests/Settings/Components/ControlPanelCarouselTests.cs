using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
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
    public async Task Should_slide_track_to_requested_control_panel()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();

        // Act
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Assert
        var track = component.Find(".control-panel-carousel__track");
        track.GetAttribute("style").Should().Be("margin-left: -100%");
        track.ClassList.Should().Contain("sliding");
    }

    [Fact]
    public async Task Should_stop_sliding_track_when_slide_ends()
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
        component.WaitForAssertion(() => component.Find(".control-panel-carousel__track").ClassList.Should().NotContain("sliding"));
        component.Find(".control-panel-carousel__track").GetAttribute("style").Should().Be("margin-left: -100%");
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
        var track = component.Find(".control-panel-carousel__track");
        track.ClassList.Should().NotContain("sliding");
        track.GetAttribute("style").Should().Be("margin-left: 0%");
        component.FindComponents<FirstLevelControlPanel>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_end_slide_immediately_when_carousel_script_is_unavailable()
    {
        // Arrange
        await using var testContext = SetupTestContext(out _, carouselModuleAvailable: false);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();

        // Act
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Assert
        component.WaitForAssertion(() => component.FindComponents<FirstLevelControlPanel>().Should().BeEmpty());
        component.FindComponents<SecondLevelControlPanel>().Should().ContainSingle();
    }

    [Fact]
    public async Task Should_end_slide_when_waiting_for_its_end_fails()
    {
        // Arrange
        await using var testContext = SetupTestContext(out var transitionEnd);

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Act
        transitionEnd.SetException(new JSException("getAnimations is not a function"));

        // Assert
        component.WaitForAssertion(() => component.FindComponents<FirstLevelControlPanel>().Should().BeEmpty());
        component.Find(".control-panel-carousel__track").ClassList.Should().NotContain("sliding");
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
        await testContext.DisposeComponentsAsync();

        // Assert
        secondLevel.State.ActivePageIndex.Should().BeNull();
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

    private static BunitContext SetupTestContext(out TaskCompletionSource<IJSVoidResult> transitionEnd, bool carouselModuleAvailable = true)
    {
        var slideEnd = new TaskCompletionSource<IJSVoidResult>();
        transitionEnd = slideEnd;

        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            var carouselModule = Substitute.For<IJSObjectReference>();
#pragma warning disable CA2012 // NSubstitute only records the call here; the returned ValueTask is never consumed.
            carouselModule.InvokeAsync<IJSVoidResult>("waitForTransitionEnd", Arg.Any<CancellationToken>(), Arg.Any<object?[]?>())
                .Returns(_ => new ValueTask<IJSVoidResult>(slideEnd.Task));
#pragma warning restore CA2012
            setup.JsInterop.IncludeModuleScript<SharedClientModule>("control-panel-carousel.js", Arg.Any<CancellationToken>())
                .Returns(carouselModuleAvailable ? carouselModule : null);

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

        [ControlPanelCategory<ControlPanelCategoryDescriptor>]
        public class TestControlPanel : ControlPanelBase<ControlPanelState>;

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

        public sealed class ThirdLevelControlPanel : TestControlPanel;

        public sealed class ThirdLevelControlPanelDescriptor : IControlPanelDescriptor<ThirdLevelControlPanel>
        {
            public string Title => "Third level";
            public Uri? IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }
    }
}
