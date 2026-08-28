using AwesomeAssertions;
using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.Services;
using Core.Shared.HostManagement.Events;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Xunit;
using MessageBannerStrings = Blazor.Shared.MessageBanner.Localization.MessageBanner;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class SystemRestartObserverTests
{
    [Theory]
    [InlineData(RestartReason.ModuleConfiguration)]
    [InlineData(RestartReason.EnvironmentConfiguration)]
    public async Task Should_show_suite_restart_required_banner(RestartReason reason)
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        var bannerMediator = Substitute.For<IMessageBannerMediator>();
        IMessage? shownMessage = null;
        bannerMediator.When(m => m.ShowMessageBanner(Arg.Any<IMessage>())).Do(c => shownMessage = c.Arg<IMessage>());

        using var observer = new SystemRestartObserver
        {
            Mediator = mediator,
            MessageBannerMediator = bannerMediator,
        };

        var message = new SystemRestartRequired(Guid.NewGuid(), reason);
        var context = new ClientContext<SystemRestartRequired>(message, Guid.NewGuid());

        // Act
        await observer.Consume(context, TestContext.Current.CancellationToken);

        // Assert
        shownMessage.Should().NotBeNull();
        shownMessage!.Title.Should().Be(MessageBannerStrings.SuiteRestartRequiredHeader);
        shownMessage.Description.Should().Be(MessageBannerStrings.SuiteRestartRequired);
    }

    [Fact]
    public async Task Should_show_system_restart_required_banner_for_system_configuration()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        var bannerMediator = Substitute.For<IMessageBannerMediator>();
        IMessage? shownMessage = null;
        bannerMediator.When(m => m.ShowMessageBanner(Arg.Any<IMessage>())).Do(c => shownMessage = c.Arg<IMessage>());

        using var observer = new SystemRestartObserver
        {
            Mediator = mediator,
            MessageBannerMediator = bannerMediator,
        };

        var message = new SystemRestartRequired(Guid.NewGuid(), RestartReason.SystemConfiguration);
        var context = new ClientContext<SystemRestartRequired>(message, Guid.NewGuid());

        // Act
        await observer.Consume(context, TestContext.Current.CancellationToken);

        // Assert
        shownMessage.Should().NotBeNull();
        shownMessage!.Title.Should().Be(MessageBannerStrings.SystemRestartRequiredHeader);
        shownMessage.Description.Should().Be(MessageBannerStrings.SystemRestartRequired);
    }
}
