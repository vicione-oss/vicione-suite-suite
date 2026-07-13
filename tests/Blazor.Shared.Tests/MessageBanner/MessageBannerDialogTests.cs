using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.Services;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerDialogTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new MessageBannerDialogState();

        using var ctx = new BunitContext();

        ctx.Services
            .AddScoped(_ => Substitute.For<IMessageBannerMediator>())
            .AddScoped(_ => state);

        // Act
        var component = ctx.Render<MessageBannerDialog>();

        // Assert
        component.Should().NotBeNull();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Should_show_or_hide_based_on_state_visibility(bool stateVisible, bool shouldBeVisible)
    {
        // Arrange
        var state = new MessageBannerDialogState { Visible = stateVisible };

        using var ctx = new BunitContext();

        ctx.Services
            .AddScoped(_ => Substitute.For<IMessageBannerMediator>())
            .AddScoped(_ => state);

        // Act
        var component = ctx.Render<MessageBannerDialog>();

        // Assert
        var assertion = component.Markup.Should();

        if (shouldBeVisible)
            assertion.NotBeEmpty();
        else
            assertion.BeEmpty();
    }

    [Fact]
    public void Should_minimize_message_banner()
    {
        // Arrange
        var state = new MessageBannerDialogState { Visible = true };
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        using var ctx = new BunitContext();

        ctx.Services
            .AddScoped(_ => messageBannerMediator)
            .AddScoped(_ => state);

        // Act
        var component = ctx.Render<MessageBannerDialog>();

        // Assert
        var minimizeButton = component.Find("button");

        minimizeButton.Click();

        // Assert
        messageBannerMediator.Received(1).MinimizeMessageBanner();
    }
}
