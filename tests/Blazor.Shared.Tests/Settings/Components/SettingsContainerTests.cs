using System.Security.Claims;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Services;
using Bunit;
using Core.Shared.UserManagement.Comparers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using Sdk.Modules;
using Xunit;

namespace Blazor.Shared.Tests.Settings.Components;

public class SettingsContainerTests
{
    public sealed class DummyClientModule : IClientModule
    {
        public ModuleKey ModuleKey => new();

        public IEnumerable<ModuleKey> Dependencies => [];

        [ControlPanelCategory<FirstControlPanelCategoryDescriptor>]
        public sealed class FirstControlPanel : ControlPanelBase<ControlPanelState>
        {
        }

        public sealed class FirstControlPanelDescriptor : IControlPanelDescriptor<FirstControlPanel>
        {
            public string Title => "Dummy";
            public string IconPath => "icon.svg";
        }

        internal sealed class FirstControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
        {
            public string Title => "About";
            public string? IconCssClass => null;
            public Uri? IconUrl => new("icon.svg", UriKind.Relative);
            public int? Position => 0;
        }

        internal sealed class SecondControlPanelCategoryDescriptor : IControlPanelCategoryDescriptor
        {
            public string Title => "Network";
            public string? IconCssClass => null;
            public Uri? IconUrl => new("network.svg", UriKind.Relative);
            public int? Position => 0;
        }

        [ControlPanelCategory<SecondControlPanelCategoryDescriptor>]
        public sealed class CloudControlPanel : ControlPanelBase<ControlPanelState>
        {
        }

        public sealed class CloudControlPanelDescriptor : IControlPanelDescriptor<CloudControlPanel>
        {
            public string Title => "Cloud";
            public string IconPath => "icon.svg";
        }
    }

    private static TestContext SetupTestContext()
    {
        var ctx = new TestContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddControlPanel<DummyClientModule, DummyClientModule.FirstControlPanel, ControlPanelState>()
                .WithAutoDiscovery<DummyClientModule.FirstControlPanelDescriptor>();

            setup.Services.AddControlPanel<DummyClientModule, DummyClientModule.CloudControlPanel, ControlPanelState>()
                .WithAutoDiscovery<DummyClientModule.CloudControlPanelDescriptor>();

            setup.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());
            setup.Services.AddSingleton(Substitute.For<IActiveControlPanelDescriptorProvider>());
            setup.Services.AddSingleton(Substitute.For<IControlPanelRequest>());
            setup.Services.AddSingleton(Substitute.For<INavigateBackRequest>());
            setup.Services.AddSingleton(Substitute.For<ILoadingIndicationPlacementBehavior>());

            setup.FakeAuthenticationStateProvider = true;
            setup.Services.AddSingleton<IEqualityComparer<Claim>, ClaimEqualityComparer>();
            setup.Services.AddScoped<IControlPanelRegistryItemCache, ControlPanelRegistryItemCache>();
        });

        return ctx;
    }

    [Fact]
    public void ComponentShouldRender()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act
        var component = ctx.RenderComponent<SettingsContainer>();

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public void CategoryClickLoadsSubCategories()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.RenderComponent<SettingsContainer>();

        var categoryTitle = "Network";

        var networkAccordionItemText = component.FindAll(".dxbl-accordion-item-text")
            .FirstOrDefault(i => i.Text() == categoryTitle);

        Assert.NotNull(networkAccordionItemText);
        networkAccordionItemText.Click();

        var updatedNetworkAccordionItem = component.FindAll(".dxbl-accordion-item")
            .FirstOrDefault(i => i.Descendants<IHtmlSpanElement>()
                .Any(span => span.ClassList.Contains("dxbl-accordion-item-text") && span.Text() == categoryTitle));
        Assert.NotNull(updatedNetworkAccordionItem);

        var menuEntries = updatedNetworkAccordionItem
            .Descendants<IHtmlDivElement>()
            .Where(menuEntry => menuEntry.ClassList.Contains("menu-entry"));

        menuEntries.Should().HaveCount(1);
    }

    [Fact]
    public void SubCategoryClickRendersComponent()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.RenderComponent<SettingsContainer>();

        var categoryTitle = "Network";

        var networkAccordionItemText = component.FindAll(".dxbl-accordion-item-text")
            .FirstOrDefault(i => i.Text() == categoryTitle);

        Assert.NotNull(networkAccordionItemText);
        networkAccordionItemText.Click();

        var updatedNetworkAccordionItem = component.FindAll(".dxbl-accordion-item")
            .FirstOrDefault(i => i.Descendants<IHtmlSpanElement>()
                .Any(span => span.ClassList.Contains("dxbl-accordion-item-text") && span.Text() == categoryTitle));
        Assert.NotNull(updatedNetworkAccordionItem);

        var cloudMenuItem = updatedNetworkAccordionItem
            .Descendants<IHtmlDivElement>()
            .FirstOrDefault(menuEntry => menuEntry.Descendants<IHtmlDivElement>().Any(div => div.Text() == "Cloud"));

        Assert.NotNull(cloudMenuItem);
        cloudMenuItem.Click();

        Assert.NotNull(component.Find(".control-panel-container"));
        Assert.NotNull(component.FindComponent<DummyClientModule.CloudControlPanel>());
    }

    [Fact]
    public void SaveAndRevertButtonHiddenOnCleanState()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.RenderComponent<SettingsContainer>();

        Assert.Empty(component.FindAll(".dirty-state"));
    }

    [Fact]
    public void SaveErrorGetsDisplayed()
    {
        // Arrange
        using var ctx = SetupTestContext();

        var panelServiceMock = ctx.Services.GetRequiredService<IControlPanelService>();

        // Act
        var component = ctx.RenderComponent<SettingsContainer>();

        if (panelServiceMock is not null)
        {
            panelServiceMock.IsDirty
                .Returns(true);

            panelServiceMock.FinishEdit()
                .Returns(new ControlPanelStateFinishResult(false, "ErrorMessage"));
        }

        component.Render();

        var saveButton = component.Find(".popup-content-action-button--confirm");
        saveButton.Click();

        // Assert
        Assert.NotNull(component.Find(".error-message"));
    }
}
