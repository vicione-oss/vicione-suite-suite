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

public sealed class CloseMessageBannerTests
{
    [Fact]
    public void Should_hide_dialog()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        using var serviceProvider = services.BuildServiceProvider();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();
        var dialogState = serviceProvider.GetRequiredService<MessageBannerDialogState>();
        dialogState.Visible = true;

        // Act
        messageBannerMediator.CloseMessageBanner();

        // Assert
        dialogState.Visible.Should().BeFalse();
    }

    [Fact]
    public void Should_hide_notification()
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
        messageBannerMediator.CloseMessageBanner();

        // Assert
        notificationElementState.Visible.Should().BeFalse();
    }

    [Fact]
    public void Should_raise_message_banner_closed()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        using var serviceProvider = services.BuildServiceProvider();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();
        var messageBannerMediatorMonitor = messageBannerMediator.Monitor();

        // Act
        messageBannerMediator.CloseMessageBanner();

        // Assert
        messageBannerMediatorMonitor.Should().Raise(nameof(messageBannerMediator.MessageBannerClosed));
    }
}
