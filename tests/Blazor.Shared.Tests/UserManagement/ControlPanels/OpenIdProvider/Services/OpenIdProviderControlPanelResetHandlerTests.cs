using AwesomeAssertions;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;
using Core.Shared.UserManagement.Requests;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.OpenIdProvider.Services;

public sealed class OpenIdProviderControlPanelResetHandlerTests
{
    private static readonly ILogger<OpenIdProviderControlPanelResetHandler> _logger
        = Substitute.For<ILogger<OpenIdProviderControlPanelResetHandler>>();

    private static OpenIdProviderControlPanelResetHandler CreateResetHandler(IUiMediator mediator)
        => new(mediator, _logger);

    [Fact]
    public async Task Should_fill_the_state_from_the_stored_provider()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetExternalIdProvider, GetExternalIdProviderResponse>(
                Arg.Any<GetExternalIdProvider>(), Arg.Any<CancellationToken>())
            .Returns(new GetExternalIdProviderResponse("https://idp.example.com", "client-id", true));

        var resetHandler = CreateResetHandler(mediator);
        var state = new OpenIdProviderControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.Authority.Should().Be("https://idp.example.com");
        state.ClientId.Should().Be("client-id");
        state.ClientSecretStored.Should().BeTrue();
        state.ClientSecret.Should().BeEmpty();
        state.LoadError.Should().BeNull();
    }

    [Fact]
    public async Task Should_discard_a_pending_secret_removal()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetExternalIdProvider, GetExternalIdProviderResponse>(
                Arg.Any<GetExternalIdProvider>(), Arg.Any<CancellationToken>())
            .Returns(new GetExternalIdProviderResponse("https://idp.example.com", "client-id", true));

        var resetHandler = CreateResetHandler(mediator);
        var state = new OpenIdProviderControlPanelState
        {
            ClientSecret = "typed",
            RemoveStoredClientSecret = true
        };

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.RemoveStoredClientSecret.Should().BeFalse();
        state.ClientSecret.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_record_the_request_error_as_a_load_error()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetExternalIdProvider, GetExternalIdProviderResponse>(
                Arg.Any<GetExternalIdProvider>(), Arg.Any<CancellationToken>())
            .Returns(new GetExternalIdProviderResponse(string.Empty, string.Empty, false,
                new ErrorInfo(1, "database unavailable")));

        var resetHandler = CreateResetHandler(mediator);
        var state = new OpenIdProviderControlPanelState();

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.LoadError.Should().Be("database unavailable");
    }

    [Fact]
    public async Task Should_fall_back_to_the_generic_message_when_the_request_throws()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetExternalIdProvider, GetExternalIdProviderResponse>(
                Arg.Any<GetExternalIdProvider>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var resetHandler = CreateResetHandler(mediator);
        var state = new OpenIdProviderControlPanelState { Authority = "https://stale.example.com" };

        // Act
        await resetHandler.Reset(state, TestContext.Current.CancellationToken);

        // Assert
        state.LoadError.Should().Be(CommonPhrases.AnUnknownErrorOccurred);
        state.Authority.Should().BeEmpty();
    }
}
