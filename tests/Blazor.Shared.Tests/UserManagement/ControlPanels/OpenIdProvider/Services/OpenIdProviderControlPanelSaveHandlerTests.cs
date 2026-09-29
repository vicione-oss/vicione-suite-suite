using AwesomeAssertions;
using Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Services;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Events;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;
using OpenIdProviderControlPanelStrings
    = Blazor.Shared.UserManagement.ControlPanels.OpenIdProvider.Localization.OpenIdProviderControlPanel;

namespace Blazor.Shared.Tests.UserManagement.ControlPanels.OpenIdProvider.Services;

public sealed class OpenIdProviderControlPanelSaveHandlerTests
{
    private static Func<SetExternalIdProvider?> AnswerWithSuccess(IUiMediator mediator,
        OpenIdProviderControlPanelSaveHandler saveHandler)
    {
        SetExternalIdProvider? sent = null;

        mediator.When(m => m.Send(Arg.Any<SetExternalIdProvider>(), Arg.Any<CancellationToken>()))
            .Do(async callInfo =>
            {
                sent = callInfo.Arg<SetExternalIdProvider>()!;

                var message = new ExternalIdProviderChanged(sent.CorrelationId);
                var context = new ClientContext<ExternalIdProviderChanged>(message, Guid.NewGuid());

                await saveHandler.Consume(context, TestContext.Current.CancellationToken);
            });

        return () => sent;
    }

    [Fact]
    public async Task Should_send_the_trimmed_provider_and_succeed_on_the_changed_event()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState
        {
            Authority = "  https://idp.example.com  ",
            ClientId = " client-id ",
            ClientSecret = "secret"
        };

        var sentCommand = AnswerWithSuccess(mediator, saveHandler);

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();

        sentCommand().Should().NotBeNull();
        sentCommand()!.Authority.Should().Be("https://idp.example.com");
        sentCommand()!.ClientId.Should().Be("client-id");
        sentCommand()!.ClientSecret.Kind.Should().Be(ClientSecretUpdateKind.Set);
        sentCommand()!.ClientSecret.Value.Should().Be("secret");
    }

    [Fact]
    public async Task Should_clear_the_secret_field_and_record_the_stored_secret_after_a_successful_save()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = "secret"
        };
        AnswerWithSuccess(mediator, saveHandler);

        // Act
        await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        state.ClientSecret.Should().BeEmpty();
        state.ClientSecretStored.Should().BeTrue();
        state.RemoveStoredClientSecret.Should().BeFalse();
    }

    [Fact]
    public async Task Should_keep_the_stored_secret_when_the_field_was_left_empty()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecretStored = true
        };

        var sentCommand = AnswerWithSuccess(mediator, saveHandler);

        // Act
        await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        sentCommand()!.ClientSecret.Kind.Should().Be(ClientSecretUpdateKind.Keep);
        state.ClientSecretStored.Should().BeTrue();
    }

    [Fact]
    public async Task Should_clear_the_stored_secret_when_the_remove_action_was_used()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecretStored = true,
            RemoveStoredClientSecret = true
        };

        var sentCommand = AnswerWithSuccess(mediator, saveHandler);

        // Act
        await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        sentCommand()!.ClientSecret.Kind.Should().Be(ClientSecretUpdateKind.Clear);
        state.ClientSecretStored.Should().BeFalse();
    }

    [Fact]
    public async Task Should_let_a_typed_secret_override_a_pending_removal()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = "new-secret",
            ClientSecretStored = true,
            RemoveStoredClientSecret = true
        };

        var sentCommand = AnswerWithSuccess(mediator, saveHandler);

        // Act
        await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        sentCommand()!.ClientSecret.Kind.Should().Be(ClientSecretUpdateKind.Set);
        sentCommand()!.ClientSecret.Value.Should().Be("new-secret");
    }

    [Fact]
    public async Task Should_send_a_removal_when_both_fields_are_empty()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState { ClientSecretStored = true };

        var sentCommand = AnswerWithSuccess(mediator, saveHandler);

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveSuccessResult>();
        sentCommand()!.Authority.Should().BeEmpty();
        sentCommand()!.ClientId.Should().BeEmpty();
        state.ClientSecretStored.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "client-id")]
    [InlineData("https://idp.example.com", "")]
    public async Task Should_refuse_a_half_filled_provider(string authority, string clientId)
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState { Authority = authority, ClientId = clientId };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be(OpenIdProviderControlPanelStrings.IncompleteProviderError);

        await mediator.DidNotReceive().Send(Arg.Any<SetExternalIdProvider>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("http://idp.example.com")]
    [InlineData("idp.example.com")]
    [InlineData("ftp://idp.example.com")]
    public async Task Should_refuse_an_authority_that_is_not_an_absolute_https_url(string authority)
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState { Authority = authority, ClientId = "client-id" };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be(OpenIdProviderControlPanelStrings.InvalidAuthorityError);

        await mediator.DidNotReceive().Send(Arg.Any<SetExternalIdProvider>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_refuse_to_save_over_a_failed_load()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState { LoadError = "boom" };

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be(OpenIdProviderControlPanelStrings.SaveBlockedByLoadFailureError);

        await mediator.DidNotReceive().Send(Arg.Any<SetExternalIdProvider>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_report_the_backend_error_and_leave_the_state_untouched()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var saveHandler = new OpenIdProviderControlPanelSaveHandler(mediator);
        var state = new OpenIdProviderControlPanelState
        {
            Authority = "https://idp.example.com",
            ClientId = "client-id",
            ClientSecret = "secret"
        };

        mediator.When(m => m.Send(Arg.Any<SetExternalIdProvider>(), Arg.Any<CancellationToken>()))
            .Do(async callInfo =>
            {
                var command = callInfo.Arg<SetExternalIdProvider>()!;
                await saveHandler.Consume(
                    new ClientContext<SetExternalIdProviderError>(
                        new(command.CorrelationId, new ErrorInfo(500, "rejected")), Guid.NewGuid()),
                    TestContext.Current.CancellationToken);
            });

        // Act
        var result = await saveHandler.Save(state, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<SaveErrorResult>()
            .Which.Message.Should().Be("rejected");

        state.ClientSecret.Should().Be("secret");
        state.ClientSecretStored.Should().BeFalse();
    }
}
