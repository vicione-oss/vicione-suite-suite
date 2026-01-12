using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.NotificationArea;
using Blazor.Shared.MessageBanner.Services;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.NotificationArea.Extensions;
using Sdk.Client.NotificationArea.Services;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerNotificationElementTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new MessageBannerNotificationElementState();
        var iconState = new MessageBannerNotificationElementIconState();

        using var ctx = new TestContext();

        ctx.Services
            .AddScoped(_ => Substitute.For<IMessageBannerMediator>())
            .AddScoped(_ => iconState);

        // Act
        var component = ctx.RenderComponent<MessageBannerNotificationElement>(
            ComponentParameter.CreateParameter(nameof(MessageBannerNotificationElement.State), state));

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public void Should_not_be_visible_by_default()
    {
        // Arrange
        using var ctx = new TestContext();

        ctx.Services
            .AddMessageBanner()
            .AddNotificationElements<SharedClientModule>();

        var registry = ctx.Services.GetRequiredService<INotificationElementRegistry<SharedClientModule>>();
        var registryItem = registry.First(i => i.ComponentType == typeof(MessageBannerNotificationElement));

        // Act
        var component = ctx.RenderComponent<MessageBannerNotificationElement>(
            ComponentParameter.CreateParameter(nameof(MessageBannerNotificationElement.State), registryItem.State));

        // Assert
        component.Markup.Should().BeEmpty();
    }

    [Fact]
    public void Should_restore_message_banner_on_click()
    {
        // Arrange
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        using var ctx = new TestContext();

        ctx.Services
            .AddScoped(_ => messageBannerMediator)
            .AddScoped<MessageBannerNotificationElementIconState>();

        var state = new MessageBannerNotificationElementState { Visible = true };

        // Act
        var notificationElement = ctx.RenderComponent<MessageBannerNotificationElement>(
            ComponentParameter.CreateParameter(nameof(MessageBannerNotificationElement.State), state));

        var button = notificationElement.Find("button");
        button.Click();

        notificationElement.Render();

        // Assert
        messageBannerMediator.Received(1).RestoreMessageBanner();
    }
}
