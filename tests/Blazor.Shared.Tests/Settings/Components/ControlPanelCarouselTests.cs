using AwesomeAssertions;
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
using Xunit;
using static Blazor.Shared.Tests.Settings.Components.ControlPanelCarouselTests.TestClientModule;

namespace Blazor.Shared.Tests.Settings.Components;

public sealed class ControlPanelCarouselTests
{
    [Fact]
    public async Task Should_not_replace_current_control_panel_on_control_panel_request()
    {
        // Arrange
        FirstLevelControlPanel.ResetInitializationCount();

        using var testContext = SetupTestContext();

        var component = testContext.RenderComponent<ControlPanelCarousel>();

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

        using var testContext = SetupTestContext();

        var component = testContext.RenderComponent<ControlPanelCarousel>();

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

    private static void SetActiveControlPanel<TControlPanel>(TestContext ctx)
        where TControlPanel : ControlPanelBase<ControlPanelState>
    {
        var settingsModuleState = ctx.Services.GetRequiredService<SettingsModuleState>();
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<TestClientModule>>();
        settingsModuleState.ActiveControlPanelRegistryItem =
            controlPanelRegistry.FirstOrDefault(i => i.ComponentType == typeof(TControlPanel));
    }

    private static TestContext SetupTestContext()
    {
        var ctx = new TestContext();
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
            public string IconPath => "icon.svg";
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
            public string IconPath => "icon.svg";
            public bool ShowInNavigation => false;
        }
    }
}
