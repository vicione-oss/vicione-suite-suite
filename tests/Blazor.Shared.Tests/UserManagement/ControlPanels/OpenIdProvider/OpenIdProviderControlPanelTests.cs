using AngleSharp.Dom;
using AwesomeAssertions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Components;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Extensions;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.ControlPanels.Services;
using Sdk.Testing.Client;
using Xunit;
using OpenIdProviderControlPanelStrings
    = Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Localization.OpenIdProviderControlPanel;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.OpenIdProvider;

public sealed class OpenIdProviderControlPanelTests
{
    private static BunitContext SetupTestContext()
    {
        var ctx = new BunitContext();

        ctx.SetupSuiteServices(setup =>
        {
            setup.Services.AddControlPanelInfrastructure()
                .AddOpenIdProviderControlPanel();
        });

        return ctx;
    }

    private static IRenderedComponent<OpenIdProviderControlPanel> RenderPanel(BunitContext ctx,
        OpenIdProviderControlPanelState state,
        Action? onBeginEdit = null)
    {
        var controlPanelRegistry = ctx.Services.GetRequiredService<IControlPanelRegistry<SharedClientModule>>().First();

        return ctx.Render<OpenIdProviderControlPanel>(builder =>
        {
            builder.Add(c => c.State, state)
                .AddCascadingValue(controlPanelRegistry);

            if (onBeginEdit is not null)
                builder.Add(c => c.OnBeginEdit, onBeginEdit);
        });
    }

    private static List<IElement> FindTextBoxes(IRenderedComponent<OpenIdProviderControlPanel> component)
        => [.. component.FindAll(".settings-field-text-box input")];

    private static IElement? FindRemoveSecretButton(IRenderedComponent<OpenIdProviderControlPanel> component)
        => component.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains(
                OpenIdProviderControlPanelStrings.RemoveStoredClientSecret, StringComparison.Ordinal));

    [Fact]
    public async Task Should_render_the_stored_provider_without_its_secret()
    {
        // Arrange
        var state = new OpenIdProviderControlPanelState
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecretStored = true
        };

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, state);

        // Assert
        var inputs = FindTextBoxes(component);
        inputs.Should().HaveCount(3);
        inputs[0].GetAttribute("value").Should().Be("https://idp.example.com");
        inputs[1].GetAttribute("value").Should().Be("client-id");
        inputs[2].GetAttribute("value").Should().BeNullOrEmpty();
        inputs[2].GetAttribute("type").Should().Be("password");

        component.Markup.Should().Contain(OpenIdProviderControlPanelStrings.ClientSecretStoredHint);
    }

    [Fact]
    public async Task Should_always_warn_about_the_consequences()
    {
        // Arrange
        // The banner renders into the settings dialog's section outlet, so the test checks its parameters.
        var state = new OpenIdProviderControlPanelState();

        await using var ctx = SetupTestContext();

        // Act
        var banner = RenderPanel(ctx, state).FindComponent<DescriptionBanner>().Instance;

        // Assert
        banner.Title.Should().Be(OpenIdProviderControlPanelStrings.PanelTitle);
        banner.IconCssClass.Should().Contain("warning");

        ctx.Render(banner.ChildContent).Markup.Should()
            .Contain(OpenIdProviderControlPanelStrings.RiskBannerContent);
    }

    [Fact]
    public async Task Should_render_the_load_error_instead_of_an_editable_form()
    {
        // Arrange
        var state = new OpenIdProviderControlPanelState { LoadError = "The provider could not be read." };

        await using var ctx = SetupTestContext();

        // Act
        var component = RenderPanel(ctx, state);

        // Assert
        component.Markup.Should().Contain("The provider could not be read.");
        FindTextBoxes(component).Should().BeEmpty();
    }

    [Fact]
    public async Task Should_only_offer_the_remove_action_while_a_secret_is_stored()
    {
        // Arrange
        var withSecret = new OpenIdProviderControlPanelState { ClientSecretStored = true };
        var withoutSecret = new OpenIdProviderControlPanelState();

        await using var ctx = SetupTestContext();

        // Act
        // Assert
        FindRemoveSecretButton(RenderPanel(ctx, withSecret)).Should().NotBeNull();
        FindRemoveSecretButton(RenderPanel(ctx, withoutSecret)).Should().BeNull();
    }

    [Fact]
    public async Task Should_mark_the_secret_for_removal_and_start_an_edit()
    {
        // Arrange
        var state = new OpenIdProviderControlPanelState { ClientSecretStored = true };

        await using var ctx = SetupTestContext();

        var beginEditRaised = false;
        var component = RenderPanel(ctx, state, () => beginEditRaised = true);

        // Act
        await FindRemoveSecretButton(component)!.ClickAsync();

        // Assert
        state.RemoveStoredClientSecret.Should().BeTrue();
        beginEditRaised.Should().BeTrue();

        component.Markup.Should().Contain(OpenIdProviderControlPanelStrings.ClientSecretRemovalPendingHint);
        FindRemoveSecretButton(component).Should().BeNull();
    }

    [Fact]
    public async Task Should_start_an_edit_when_the_authority_changes()
    {
        // Arrange
        var state = new OpenIdProviderControlPanelState();

        await using var ctx = SetupTestContext();

        var beginEditRaised = false;
        var component = RenderPanel(ctx, state, () => beginEditRaised = true);

        // Act
        var authorityInput = FindTextBoxes(component)[0];
        await authorityInput.InputAsync(new ChangeEventArgs { Value = "https://idp.example.com" });
        await authorityInput.BlurAsync();

        // Assert
        state.Authority.Should().Be("https://idp.example.com");
        beginEditRaised.Should().BeTrue();
    }
}
