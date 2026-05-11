using Blazor.Shared.UserInterface.ControlPanels.Language.Services;
using Bunit;
using Bunit.TestDoubles;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Requests;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Xunit;

namespace Blazor.Shared.Tests.UserInterface.ControlPanels.Language.Services;

public class LanguageControlPanelSaveHandlerTests
{
    public class Save
    {
        private readonly IUiMediator _clientMediator = Substitute.For<IUiMediator>();
        private Guid _correlationId = Guid.Empty;

        [Fact]
        public async Task Save_Successful()
        {
            // Arrange
            await using var context = new BunitContext();
            context.Services.AddSingleton(_clientMediator);
            context.Services.AddScoped<LanguageControlPanelSaveHandler>();
            var authContext = context.AddAuthorization();
            using var saveHandler = context.Services.GetRequiredService<LanguageControlPanelSaveHandler>();

            var state = new LanguageControlPanelState
            {
                CrossInstanceConfiguration = new CrossInstanceConfiguration
                {
                    CultureName = "en-US",
                },
                ShowPageRefreshInformation = false,
            };

            _clientMediator.When(m => m.Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<SetCrossInstanceConfiguration>();
                    _correlationId = command.CorrelationId;
                    var configuration = state.CrossInstanceConfiguration;

                    var message = new CrossInstanceConfigurationChanged(_correlationId, configuration);
                    var context = new ClientContext<CrossInstanceConfigurationChanged>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, Xunit.TestContext.Current.CancellationToken);
                });

            // Act
            var handlerResult = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

            // Assert
            await _clientMediator.Received().Send(Arg.Is<SetCrossInstanceConfiguration>(a => a.CorrelationId == _correlationId), Arg.Any<CancellationToken>());

            Assert.True(state.ShowPageRefreshInformation);
            Assert.False(state.ShowLanguageDoesNotAffectCurrentUser);
            Assert.IsType<SaveSuccessResult>(handlerResult);
        }

        [Fact]
        public async Task Should_display_not_affected_banner_if_user_has_language_set()
        {
            // Arrange
            await using var context = new BunitContext();
            context.Services.AddSingleton(_clientMediator);
            context.Services.AddScoped<LanguageControlPanelSaveHandler>();
            var authContext = context.AddAuthorization();
            authContext.SetAuthorized("TEST USER", AuthorizationState.Authorized);
            SetupUserRequest("TEST USER", "en-GB");

            using var saveHandler = context.Services.GetRequiredService<LanguageControlPanelSaveHandler>();

            var state = new LanguageControlPanelState
            {
                CrossInstanceConfiguration = new CrossInstanceConfiguration
                {
                    CultureName = "en-US",
                },
                ShowPageRefreshInformation = false,
            };

            _clientMediator.When(m => m.Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<SetCrossInstanceConfiguration>();
                    _correlationId = command.CorrelationId;
                    var configuration = state.CrossInstanceConfiguration;

                    var message = new CrossInstanceConfigurationChanged(_correlationId, configuration);
                    var context = new ClientContext<CrossInstanceConfigurationChanged>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, Xunit.TestContext.Current.CancellationToken);
                });

            // Act
            var handlerResult = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

            // Assert
            await _clientMediator.Received().Send(Arg.Is<SetCrossInstanceConfiguration>(a => a.CorrelationId == _correlationId), Arg.Any<CancellationToken>());

            Assert.False(state.ShowPageRefreshInformation);
            Assert.True(state.ShowLanguageDoesNotAffectCurrentUser);
            Assert.IsType<SaveSuccessResult>(handlerResult);
        }

        [Fact]
        public async Task Save_Failed()
        {
            // Arrange
            var errorMessage = "Error occurred.";
            await using var context = new BunitContext();
            context.Services.AddSingleton(_clientMediator);
            context.Services.AddScoped<LanguageControlPanelSaveHandler>();
            var authContext = context.AddAuthorization();
            using var saveHandler = context.Services.GetRequiredService<LanguageControlPanelSaveHandler>();

            var state = new LanguageControlPanelState
            {
                CrossInstanceConfiguration = new CrossInstanceConfiguration
                {
                    CultureName = "en-US",
                },
                ShowPageRefreshInformation = false,
            };

            _clientMediator.When(m => m.Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<SetCrossInstanceConfiguration>();
                    _correlationId = command.CorrelationId;

                    var errorInfo = new ErrorInfo(CrossInstanceConfigurationError.AddOrUpdateFailed, errorMessage);

                    var message = new CrossInstanceConfigurationError(_correlationId, errorInfo, Guid.NewGuid());
                    var context = new ClientContext<CrossInstanceConfigurationError>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, Xunit.TestContext.Current.CancellationToken);
                });

            // Act
            var handlerResult = await saveHandler.Save(state, Xunit.TestContext.Current.CancellationToken);

            // Assert
            await _clientMediator.Received().Send(Arg.Is<SetCrossInstanceConfiguration>(a => a.CorrelationId == _correlationId), Arg.Any<CancellationToken>());

            Assert.False(state.ShowPageRefreshInformation);
            Assert.IsType<SaveErrorResult>(handlerResult);
            Assert.Equal(errorMessage, handlerResult.Message);
            Assert.Equal(CrossInstanceConfigurationError.AddOrUpdateFailed, ((SaveErrorResult)handlerResult).ErrorCode);
        }

        private void SetupUserRequest(string userName, string? language = null)
        {
            var userProfile = new UserProfile
            {
                UserName = new UserName(userName),
                Email = "",
                Language = language,
            };

            _clientMediator.Request<GetUsers, GetUsersResponse>(new GetUsers(new(userName)), Arg.Any<CancellationToken>())
                .Returns(new GetUsersResponse([userProfile]));
        }
    }
}
