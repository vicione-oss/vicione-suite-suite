using System.Security.Claims;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Blazor.Shared.Popup.Services;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Bunit;
using Core.Shared.UserManagement.Comparers;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Extensions;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;
using Sdk.Modules;

namespace Blazor.Shared.Tests.Settings.Components;

public sealed class SettingsContainerTests
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
    public async Task Should_render_component()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var component = ctx.Render<SettingsContainer>();

        // Assert
        component.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_load_sub_categories_on_category_click()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.Render<SettingsContainer>();

        var categoryTitle = "Network";

        var networkAccordionItemText = component.FindAll(".accordion-item .header .text")
            .FirstOrDefault(i => i.Text() == categoryTitle);

        Assert.NotNull(networkAccordionItemText);
        await networkAccordionItemText.ClickAsync();

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
    public async Task Should_render_component_on_sub_category_click()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.Render<SettingsContainer>();

        var categoryTitle = "Network";

        var networkAccordionItemText = component.FindAll(".accordion-item .header .text")
            .FirstOrDefault(i => i.Text() == categoryTitle);

        Assert.NotNull(networkAccordionItemText);
        await networkAccordionItemText.ClickAsync();

        var updatedNetworkAccordionItem = component.FindAll(".accordion-item")
            .FirstOrDefault(i => i.Descendants<IHtmlSpanElement>()
                .Any(span => span.ClassList.Contains("text") && span.Text() == categoryTitle));
        Assert.NotNull(updatedNetworkAccordionItem);

        var cloudMenuItem = updatedNetworkAccordionItem
            .Descendants<IHtmlButtonElement>()
            .FirstOrDefault(menuEntry => menuEntry.ClassList.Contains("menu-entry") &&
                menuEntry.Descendants<IHtmlDivElement>().Any(div => div.Text() == "Cloud"));

        Assert.NotNull(cloudMenuItem);
        await cloudMenuItem.ClickAsync();

        component.Find(".control-panel-container").Should().NotBeNull();
        component.FindComponent<DummyClientModule.CloudControlPanel>().Should().NotBeNull();
    }

    [Fact]
    public async Task Should_hide_save_and_revert_buttons_on_clean_state()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act + Assert
        var component = ctx.Render<SettingsContainer>();

        component.FindAll(".dirty-state").Should().BeEmpty();
    }

    [Fact]
    public async Task Should_display_save_error()
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
        component.Find(".error-message").Should().NotBeNull();
    }

    [Fact]
    public async Task Should_request_navigate_back_after_save_with_navigate_back_result()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var component = await SaveControlPanelEditWithResult(ctx, new NavigateBackOnSaveSuccessResult());

        // Assert
        var navigateBackRequest = ctx.Services.GetRequiredService<INavigateBackRequest>();
        component.WaitForAssertion(() => _ = navigateBackRequest.Received(1).Send());
    }

    [Fact]
    public async Task Should_not_request_navigate_back_after_plain_successful_save()
    {
        // Arrange
        await using var ctx = SetupTestContext();

        // Act
        var component = await SaveControlPanelEditWithResult(ctx, new SaveSuccessResult());

        // Assert
        component.WaitForAssertion(() => Assert.Empty(component.FindAll(".dirty-state")));
        _ = ctx.Services.GetRequiredService<INavigateBackRequest>().DidNotReceive().Send();
    }

    private static async Task<IRenderedComponent<SettingsContainer>> SaveControlPanelEditWithResult(BunitContext ctx, ISaveResult saveResult)
    {
        ctx.Services.AddScoped<IControlPanelEditRegistry, ControlPanelEditRegistry>();

        var saveHandler = Substitute.For<IControlPanelSaveHandler<ControlPanelState>>();
        saveHandler.Save(Arg.Any<ControlPanelState>(), Arg.Any<CancellationToken>())
            .Returns(saveResult);
        ctx.Services.AddSingleton(saveHandler);

        var component = ctx.Render<SettingsContainer>();

        var controlPanelEditRegistry = ctx.Services.GetRequiredService<IControlPanelEditRegistry>();
        controlPanelEditRegistry.Single().Begin();

        component.Render();

        var saveButton = component.Find(".popup-content-action-button--confirm");
        await saveButton.ClickAsync();

        return component;
    }
}
