using System.Security.Claims;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AwesomeAssertions;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Bunit;
using Core.Shared.UserManagement.Comparers;
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

        [ControlPanelCategory<FirstControlPanelCategoryDescriptor>]
        public sealed class FirstControlPanel : ControlPanelBase<ControlPanelState>
        {
        }

        public sealed class FirstControlPanelDescriptor : IControlPanelDescriptor<FirstControlPanel>
        {
            public string Title => "Dummy";
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
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
            public Uri IconUrl => new("icon.svg", UriKind.Relative);
        }
    }

    private static BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddControlPanelInfrastructure();

            setup.Services.AddControlPanel<DummyClientModule, DummyClientModule.FirstControlPanel, ControlPanelState>()
                .WithAutoDiscovery<DummyClientModule.FirstControlPanelDescriptor>();

            setup.Services.AddControlPanel<DummyClientModule, DummyClientModule.CloudControlPanel, ControlPanelState>()
                .WithAutoDiscovery<DummyClientModule.CloudControlPanelDescriptor>();

            setup.Services.AddSingleton(Substitute.For<INavigateBackRequest>());
            setup.Services.AddSingleton(Substitute.For<ILoadingIndicationPlacementBehavior>());

            setup.FakeAuthenticationStateProvider = true;
            setup.Services.AddSingleton<IEqualityComparer<Claim>, ClaimEqualityComparer>();
        });

        return ctx;
    }

    [Fact]
    public void ComponentShouldRender()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act
        var component = ctx.Render<SettingsContainer>();

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public void CategoryClickLoadsSubCategories()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.Render<SettingsContainer>();

        var categoryTitle = "Network";

        var networkAccordionItemText = component.FindAll(".accordion-item .header .text")
            .FirstOrDefault(i => i.Text() == categoryTitle);

        Assert.NotNull(networkAccordionItemText);
        networkAccordionItemText.Click();

        var updatedNetworkAccordionItem = component.FindAll(".accordion-item")
            .FirstOrDefault(i => i.Descendants<IHtmlSpanElement>()
                .Any(span => span.ClassList.Contains("text") && span.Text() == categoryTitle));
        Assert.NotNull(updatedNetworkAccordionItem);

        var menuEntries = updatedNetworkAccordionItem
            .Descendants<IHtmlButtonElement>()
            .Where(menuEntry => menuEntry.ClassList.Contains("menu-entry"));

        menuEntries.Should().HaveCount(1);
    }

    [Fact]
    public void SubCategoryClickRendersComponent()
    {
        // Arrange
        using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.Render<SettingsContainer>();

        var categoryTitle = "Network";

        var networkAccordionItemText = component.FindAll(".accordion-item .header .text")
            .FirstOrDefault(i => i.Text() == categoryTitle);

        Assert.NotNull(networkAccordionItemText);
        networkAccordionItemText.Click();

        var updatedNetworkAccordionItem = component.FindAll(".accordion-item")
            .FirstOrDefault(i => i.Descendants<IHtmlSpanElement>()
                .Any(span => span.ClassList.Contains("text") && span.Text() == categoryTitle));
        Assert.NotNull(updatedNetworkAccordionItem);

        var cloudMenuItem = updatedNetworkAccordionItem
            .Descendants<IHtmlButtonElement>()
            .FirstOrDefault(menuEntry => menuEntry.ClassList.Contains("menu-entry") &&
                menuEntry.Descendants<IHtmlDivElement>().Any(div => div.Text() == "Cloud"));

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
        var component = ctx.Render<SettingsContainer>();

        Assert.Empty(component.FindAll(".dirty-state"));
    }

    [Fact]
    public async Task SaveErrorGetsDisplayed()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        ctx.Services.AddScoped<IControlPanelEditRegistry, ControlPanelEditRegistry>();

        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry>();
        var controlPanelState = controlPanelRegistry.First().State as ControlPanelState;

        await using var controlPanelEdit = new ControlPanelEdit<ControlPanelState>(controlPanelState!, ctx.Services);
        var controlPanelEditRegistry = ctx.Services.GetRequiredService<IControlPanelEditRegistry>();
        controlPanelEditRegistry.Add(controlPanelEdit);

        // Act
        var component = ctx.Render<SettingsContainer>();

        controlPanelEdit.Begin();

        component.Render();

        var saveButton = component.Find(".popup-content-action-button--confirm");
        await saveButton.ClickAsync();

        // Assert
        Assert.NotNull(component.Find(".error-message"));
    }
}
