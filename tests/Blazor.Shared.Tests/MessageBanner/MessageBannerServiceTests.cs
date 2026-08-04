using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MessageBannerServiceTests
{
    public static readonly TheoryData<MessageType> MessageTypes = new(Enum.GetValues<MessageType>());

    [Fact]
    public void Should_be_resolvable()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddMessageBanner()
            .AddScoped(serviceProvider => Substitute.For<IMessageBannerMediator>());

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var messageBannerService = serviceProvider.GetService<IMessageBannerService>();

        // Assert
        messageBannerService.Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(MessageTypes))]
    public void Should_pass_show_message_banner_to_mediator(MessageType messageType)
    {
        // Arrange
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        var services = new ServiceCollection()
            .AddMessageBanner()
            .AddScoped(serviceProvider => messageBannerMediator);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var messageBannerService = serviceProvider.GetRequiredService<IMessageBannerService>();

        // Assert
        var description = messageType.ToString();

        messageBannerService.ShowMessageBanner(messageType, description);

        messageBannerMediator.Received(1).ShowMessageBanner(Arg.Is<IMessage>(message =>
            message!.Type == messageType &&
            message.Description == description &&
            message.Icon == messageType.ToIcon() &&
            message.Title == messageType.ToTitle()));
    }

    [Fact]
    public void Should_pass_close_message_banner_to_mediator()
    {
        // Arrange
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        var services = new ServiceCollection()
            .AddMessageBanner()
            .AddScoped(serviceProvider => messageBannerMediator);

        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var messageBannerService = serviceProvider.GetRequiredService<IMessageBannerService>();

        // Assert
        messageBannerService.CloseMessageBanner();

        messageBannerMediator.Received(1).CloseMessageBanner();
    }

    [Fact]
    public void Should_raise_message_banner_closed()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddMessageBanner()
            .AddScoped<IMessageBannerMediator, TestMessageBannerMediator>();

        using var serviceProvider = services.BuildServiceProvider();

        var messageBannerService = serviceProvider.GetRequiredService<IMessageBannerService>();
        var messageBannerServiceMonitor = messageBannerService.Monitor();

        var messageBannerMediator = serviceProvider.GetRequiredService<IMessageBannerMediator>();

        // Act
        messageBannerMediator.CloseMessageBanner();

        // Assert
        messageBannerServiceMonitor.Should().Raise(nameof(messageBannerService.MessageBannerClosed));
    }
}
