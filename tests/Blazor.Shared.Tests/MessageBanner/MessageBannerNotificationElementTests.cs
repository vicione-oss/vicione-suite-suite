using AwesomeAssertions;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.NotificationArea;
using Blazor.Shared.MessageBanner.Services;
using Blazor.Shared.NotificationArea.Extensions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.NotificationArea.Extensions;
using Sdk.Client.NotificationArea.Services;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerNotificationElementTests
{
    [Fact]
    public async Task Should_render_component()
    {
        // Arrange
        var state = new MessageBannerNotificationElementState();
        var iconState = new MessageBannerNotificationElementIconState();

        await using var ctx = new BunitContext();

        ctx.Services
            .AddScoped(_ => Substitute.For<IMessageBannerMediator>())
            .AddScoped(_ => iconState);

        // Act
        var component = ctx.Render<MessageBannerNotificationElement>(
            c => c.Add(p => p.State, state));

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public async Task Should_not_be_visible_by_default()
    {
        // Arrange
       await using var ctx = new BunitContext();

        ctx.Services
            .AddMessageBanner()
            .AddNotificationElementInfrastructure()
            .AddNotificationElements<SharedClientModule>();

        var registry = ctx.Services.GetRequiredService<INotificationElementRegistry<SharedClientModule>>();
        var registryItem = registry.First(i => i.ComponentType == typeof(MessageBannerNotificationElement));

        // Act
        var component = ctx.Render<MessageBannerNotificationElement>(
            c => c.Add(p => p.State, registryItem.State));

        // Assert
        component.Markup.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_restore_message_banner_on_click()
    {
        // Arrange
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        await using var ctx = new BunitContext();

        ctx.Services
            .AddScoped(_ => messageBannerMediator)
            .AddScoped<MessageBannerNotificationElementIconState>();

        var state = new MessageBannerNotificationElementState { Visible = true };

        // Act
        var notificationElement = ctx.Render<MessageBannerNotificationElement>(
            c => c.Add(p => p.State, state));


        var button = notificationElement.Find("button");
        await button.ClickAsync();

        notificationElement.Render();

        // Assert
        messageBannerMediator.Received(1).RestoreMessageBanner();
    }
}
