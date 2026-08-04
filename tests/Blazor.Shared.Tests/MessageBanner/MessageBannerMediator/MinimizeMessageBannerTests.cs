using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.NotificationArea;
using Blazor.Shared.MessageBanner.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea;
using Sdk.Client.NotificationArea.Extensions;

namespace Blazor.Shared.Tests.MessageBanner.MessageBannerMediator;

public sealed class MinimizeMessageBannerTests
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
        messageBannerMediator.MinimizeMessageBanner();

        // Assert
        dialogState.Visible.Should().BeFalse();
    }


    [Fact]
    public void Should_show_notification_element()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        using var serviceProvider = services.BuildServiceProvider();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();
        var notificationElementState = serviceProvider.GetRequiredKeyedService<MessageBannerNotificationElementState>(
            typeof(NotificationElementServiceKey<SharedClientModule, MessageBannerNotificationElement>));
        notificationElementState.Visible = false;

        // Act
        messageBannerMediator.MinimizeMessageBanner();

        // Assert
        notificationElementState.Visible.Should().BeTrue();
    }
}
