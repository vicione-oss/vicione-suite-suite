using AwesomeAssertions;
using Blazor.Server.Backend.Components;
using Blazor.Shared.Profile.Models;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Blazor.Server.Tests.Components;

public sealed class PasskeySubmitTests
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
        button.HasAttribute("disabled").Should().BeTrue();
        button.GetAttribute("title").Should().Be(TooltipText);
        button.GetAttribute("aria-label").Should().Be(TooltipText);
        component.FindAll("passkey-submit").Should().BeEmpty();
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
        component.FindAll("passkey-submit").Should().NotBeEmpty();
        component.Find("button").HasAttribute("disabled").Should().BeFalse();
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(new ResourceAssetCollection([new ResourceAsset("js/passkey-submit.js", [])]));
        return ctx;
    }
}
