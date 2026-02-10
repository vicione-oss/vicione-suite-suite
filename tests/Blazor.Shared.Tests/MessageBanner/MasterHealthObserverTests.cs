using Blazor.Shared.Enums;
using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.MessageBanner.Models;
using Blazor.Shared.MessageBanner.Services;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Instance.HealthCheck.Events;
using Sdk.MessageBanner.Contracts;
using Xunit;

namespace Blazor.Shared.Tests.MessageBanner;

public sealed class MasterHealthObserverTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => Substitute.For<IUiMediator>());
        ctx.Services.AddScoped(_ => Substitute.For<IMessageBannerMediator>());

        // Act
        var renderedComponent = ctx.Render<MasterHealthObserver>();

        // Assert
        renderedComponent.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_call_show_message_banner()
    {
        // Arrange
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        await using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => Substitute.For<IUiMediator>());
        ctx.Services.AddScoped(_ => messageBannerMediator);

        var renderedComponent = ctx.Render<MasterHealthObserver>();

        // Act
        await renderedComponent.Instance.Consume(
            new ClientContext<MasterHealthInfoChanged>(new MasterHealthInfoChanged(false), Guid.NewGuid()),
            Xunit.TestContext.Current.CancellationToken);

        // Assert
        var expectedMessageType = MessageType.Warning;

        messageBannerMediator.Received(1).ShowMessageBanner(Arg.Is<IMessage>(message =>
            message.Type == expectedMessageType &&
            message.Description == Shared.MessageBanner.Localization.MessageBanner.MasterNotReachable &&
            message.Icon == SvgIcon.Offline &&
            message.Title == expectedMessageType.ToTitle()));
    }

    [Fact]
    public async Task Should_call_close_message_banner()
    {
        // Arrange
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        await using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => Substitute.For<IUiMediator>());
        ctx.Services.AddScoped(_ => messageBannerMediator);

        var renderedComponent = ctx.Render<MasterHealthObserver>();

        await renderedComponent.Instance.Consume(
            new ClientContext<MasterHealthInfoChanged>(new MasterHealthInfoChanged(false), Guid.NewGuid()),
            Xunit.TestContext.Current.CancellationToken);

        // Act
        await renderedComponent.Instance.Consume(
            new ClientContext<MasterHealthInfoChanged>(new MasterHealthInfoChanged(true), Guid.NewGuid()),
            Xunit.TestContext.Current.CancellationToken);

        // Assert
        messageBannerMediator.Received(1).CloseMessageBanner();
    }

    [Fact]
    public async Task Should_not_call_close_message_banner_because_show_message_banner_not_called_before()
    {
        // Arrange
        var messageBannerMediator = Substitute.For<IMessageBannerMediator>();

        await using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => Substitute.For<IUiMediator>());
        ctx.Services.AddScoped(_ => messageBannerMediator);

        var renderedComponent = ctx.Render<MasterHealthObserver>();

        // Act
        await renderedComponent.Instance.Consume(
            new ClientContext<MasterHealthInfoChanged>(new MasterHealthInfoChanged(true), Guid.NewGuid()),
            Xunit.TestContext.Current.CancellationToken);

        // Assert
        messageBannerMediator.DidNotReceive().CloseMessageBanner();
    }

    [Fact]
    public async Task Should_not_call_close_message_banner_because_show_message_banner_was_called_externally()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddScoped(_ => Substitute.For<IUiMediator>());
        ctx.Services.AddScoped<IMessageBannerMediator, TestMessageBannerMediator>();

        var renderedComponent = ctx.Render<MasterHealthObserver>();

        var closeMessageBannerCalled = false;

        var messageBannerMediator = ctx.Services.GetRequiredService<IMessageBannerMediator>();
        messageBannerMediator.MessageBannerClosed += () => closeMessageBannerCalled = true;

        // Act
        {
            // master goes offline
            await renderedComponent.Instance.Consume(
                new ClientContext<MasterHealthInfoChanged>(new MasterHealthInfoChanged(false), Guid.NewGuid()),
                Xunit.TestContext.Current.CancellationToken);

            // external call
            messageBannerMediator.ShowMessageBanner(new DummyMessage());

            // master comes online again
            await renderedComponent.Instance.Consume(
                new ClientContext<MasterHealthInfoChanged>(new MasterHealthInfoChanged(true), Guid.NewGuid()),
                Xunit.TestContext.Current.CancellationToken);
        }

        // Assert
        closeMessageBannerCalled.Should().BeFalse();
    }
}
