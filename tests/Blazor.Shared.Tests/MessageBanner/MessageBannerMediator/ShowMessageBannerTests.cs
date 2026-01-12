using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.NotificationArea;
using Blazor.Shared.MessageBanner.Services;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea;
using Sdk.Client.NotificationArea.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner.MessageBannerMediator;

public sealed class ShowMessageBannerTests
{
    [Fact]
    public void Should_show_dialog()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        var serviceProvider = services.BuildServiceProvider();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();
        var dialogState = serviceProvider.GetRequiredService<MessageBannerDialogState>();
        dialogState.Visible = false;

        var message = new Message();

        // Act
        messageBannerMediator.ShowMessageBanner(message);

        // Assert
        dialogState.Visible.Should().BeTrue();
        dialogState.Message.Equals(message);
    }

    [Fact]
    public void Should_hide_notification_element()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        var serviceProvider = services.BuildServiceProvider();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();
        var notificationElementState = serviceProvider.GetRequiredKeyedService<MessageBannerNotificationElementState>(
            typeof(NotificationElementServiceKey<SharedClientModule, MessageBannerNotificationElement>));
        notificationElementState.Visible = true;

        var message = new Message();

        // Act
        messageBannerMediator.ShowMessageBanner(message);

        // Assert
        notificationElementState.Visible.Should().BeFalse();
    }

    [Fact]
    public void Should_raise_message_banner_shown()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        var serviceProvider = services.BuildServiceProvider();

        IMessage? messageBannerShownMessage = null;

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();

        messageBannerMediator.MessageBannerShown += message => messageBannerShownMessage = message;
        var message = new DummyMessage();

        // Act
        messageBannerMediator.ShowMessageBanner(message);

        // Assert
        messageBannerShownMessage.Should().NotBeNull();
        messageBannerShownMessage.Should().BeEquivalentTo(message);
    }
}
