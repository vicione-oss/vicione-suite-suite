using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.Services;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Extensions;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner.MessageBannerMediator;

public sealed class ResolveTests
{
    [Fact]
    public void Should_be_resolvable()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddNotificationElements<SharedClientModule>()
            .AddMessageBanner();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var messageBannerMediator = serviceProvider.GetService<IMessageBannerMediator>();

        // Assert
        messageBannerMediator.Should().NotBeNull();
    }
}
