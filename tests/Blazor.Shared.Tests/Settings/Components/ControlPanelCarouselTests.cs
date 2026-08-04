using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
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

        await using var testContext = SetupTestContext();

        var component = testContext.Render<ControlPanelCarousel>();

        var controlPanelRequest = testContext.Services.GetRequiredService<IControlPanelRequest>();
        await controlPanelRequest.Send<FirstLevelControlPanel>();

        SetActiveControlPanel<FirstLevelControlPanel>(testContext);

        // Act
        await controlPanelRequest.Send<SecondLevelControlPanel>();

        // Assert
        FirstLevelControlPanel.InitializationCount.Should().Be(1);

        component.FindComponent<SecondLevelControlPanel>();
    }

    [Fact]
    public async Task Should_not_replace_current_control_panel_on_navigate_back()
    {
        // Arrange
        SecondLevelControlPanel.ResetInitializationCount();

        await using var testContext = SetupTestContext();

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

        component.FindComponent<FirstLevelControlPanel>();
    }

    [Fact]
    public async Task Should_navigate_back_when_requested_before_forward_transition_committed_active_panel()
    {
        // Arrange - reproduce the state a near-instant save produces: two panels are on the requested stack
        // but the forward transition has not yet committed the active panel (it is still uncommitted), so a
        // navigate-back arrives before ActiveControlPanelRegistryItem has advanced to the top of the stack.
        // The stack is populated directly (rather than via ControlPanelRequest) so no forward transitions are
        // in flight and the single navigate-back transition drains deterministically.
        await using var testContext = SetupTestContext();

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

        // Assert - the navigate-back is honored (second level popped) rather than silently dropped. The timeout
        // is generous because the pop is applied by the carousel's delayed (animation) OnAfterRender.
        component.WaitForAssertion(
            () => settingsModuleState.RequestedControlPanelRegistryItems.Should().ContainSingle()
                .Which.Should().Be(firstLevel),
            TimeSpan.FromSeconds(5));
    }

    private static void SetActiveControlPanel<TControlPanel>(BunitContext ctx)
        where TControlPanel : ControlPanelBase<ControlPanelState>
    {
        var settingsModuleState = ctx.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();
        settingsModuleState.ActiveControlPanelRegistryItem =
            controlPanelRegistry.FirstOrDefault(i => i.ComponentType == typeof(TControlPanel));
    }

    private static BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddSettings();

            setup.Services.AddControlPanel<TestClientModule, FirstLevelControlPanel, ControlPanelState>()
                    .WithAutoDiscovery<FirstLevelControlPanelDescriptor>();

            setup.Services.AddControlPanel<TestClientModule, SecondLevelControlPanel, ControlPanelState>()
                    .WithAutoDiscovery<SecondLevelControlPanelDescriptor>();
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
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
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

            public static void ResetInitializationCount()
                => _initializationCount = 0;
        }

        public sealed class SecondLevelControlPanelDescriptor : IControlPanelDescriptor<SecondLevelControlPanel>
        {
            public string Title => "Second level";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
            public bool ShowInNavigation => false;
        }
    }
}
