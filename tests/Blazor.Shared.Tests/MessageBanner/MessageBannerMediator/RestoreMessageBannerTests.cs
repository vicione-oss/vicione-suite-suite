using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.NotificationArea;
using Blazor.Shared.MessageBanner.Services;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea;
using Sdk.Client.NotificationArea.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner.MessageBannerMediator;

public sealed class RestoreMessageBannerTests
{
    [Fact]
    public void Should_show_dialog()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        using var serviceProvider = services.BuildServiceProvider();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();
        var dialogState = serviceProvider.GetRequiredService<MessageBannerDialogState>();

        var message = new DummyMessage();

        messageBannerMediator.ShowMessageBanner(message);
        messageBannerMediator.MinimizeMessageBanner();

        // Act
        messageBannerMediator.RestoreMessageBanner();

        // Assert
        dialogState.Visible.Should().BeTrue();
        dialogState.Message.Should().BeEquivalentTo(message);
    }

    [Fact]
    public void Should_hide_notification_element()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        using var serviceProvider = services.BuildServiceProvider();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();
        var notificationElementState = serviceProvider.GetRequiredKeyedService<MessageBannerNotificationElementState>(
            typeof(NotificationElementServiceKey<SharedClientModule, MessageBannerNotificationElement>));
        notificationElementState.Visible = true;

        // Act
        messageBannerMediator.RestoreMessageBanner();

        // Assert
        notificationElementState.Visible.Should().BeFalse();
    }
}
