using Blazor.Server.Backend.Components;
using Blazor.Shared.Profile.Models;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blazor.Server.Tests.Components;

public class PasskeySubmitTests
{
    private const string TooltipText = "Passkeys require a DNS hostname.";

    [Fact]
    public void Should_render_disabled_button_with_tooltip_and_no_interactive_element_when_disabled()
    {
        // Arrange
        using var ctx = CreateContext();

        // Act
        var component = ctx.Render<PasskeySubmit>(builder => builder
            .Add(c => c.Operation, PasskeyOperation.Request)
            .Add(c => c.Name, "Input.Passkey")
            .Add(c => c.Text, "Login with passkey")
            .Add(c => c.Disabled, true)
            .Add(c => c.DisabledTitle, TooltipText));

        // Assert
        var button = component.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.Equal(TooltipText, button.GetAttribute("title"));
        Assert.Equal(TooltipText, button.GetAttribute("aria-label"));
        Assert.Empty(component.FindAll("passkey-submit"));
    }

    [Fact]
    public void Should_render_interactive_element_when_enabled()
    {
        // Arrange
        using var ctx = CreateContext();

        // Act
        var component = ctx.Render<PasskeySubmit>(builder => builder
            .Add(c => c.Operation, PasskeyOperation.Request)
            .Add(c => c.Name, "Input.Passkey")
            .Add(c => c.Text, "Login with passkey")
            .Add(c => c.Disabled, false));

        // Assert
        Assert.NotEmpty(component.FindAll("passkey-submit"));
        Assert.False(component.Find("button").HasAttribute("disabled"));
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(new ResourceAssetCollection([new ResourceAsset("js/passkey-submit.js", [])]));
        return ctx;
    }
}
