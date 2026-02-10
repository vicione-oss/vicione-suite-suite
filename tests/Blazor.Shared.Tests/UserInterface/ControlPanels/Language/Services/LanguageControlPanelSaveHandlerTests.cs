using Blazor.Shared.UserInterface.ControlPanels.Language.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
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
            using var saveHandler = new LanguageControlPanelSaveHandler(_clientMediator);

            var state = new LanguageControlPanelState
            {
                CrossInstanceConfiguration = new CrossInstanceConfiguration
                {
                    CultureName = "en-US",
                },
                ShowLanguageSavedBanner = false,
            };

            _clientMediator.When(m => m.Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<SetCrossInstanceConfiguration>();
                    _correlationId = command.CorrelationId;
                    var configuration = state.CrossInstanceConfiguration;

                    var message = new CrossInstanceConfigurationChanged(_correlationId, configuration);
                    var context = new ClientContext<CrossInstanceConfigurationChanged>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            // Act
            var handlerResult = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            await _clientMediator.Received().Send(Arg.Is<SetCrossInstanceConfiguration>(a => a.CorrelationId == _correlationId), Arg.Any<CancellationToken>());

            Assert.True(state.ShowLanguageSavedBanner);
            Assert.IsType<SaveSuccessResult>(handlerResult);
        }

        [Fact]
        public async Task Save_Failed()
        {
            // Arrange
            var errorMessage = "Error occured.";
            using var saveHandler = new LanguageControlPanelSaveHandler(_clientMediator);

            var state = new LanguageControlPanelState
            {
                CrossInstanceConfiguration = new CrossInstanceConfiguration
                {
                    CultureName = "en-US",
                },
                ShowLanguageSavedBanner = false,
            };

            _clientMediator.When(m => m.Send(Arg.Any<SetCrossInstanceConfiguration>(), Arg.Any<CancellationToken>()))
                .Do(async callinfo =>
                {
                    var command = callinfo.Arg<SetCrossInstanceConfiguration>();
                    _correlationId = command.CorrelationId;

                    var errorInfo = new ErrorInfo(CrossInstanceConfigurationError.AddOrUpdateFailed, errorMessage);

                    var message = new CrossInstanceConfigurationError(_correlationId, errorInfo, Guid.NewGuid());
                    var context = new ClientContext<CrossInstanceConfigurationError>(message, Guid.NewGuid());

                    await saveHandler.Consume(context, TestContext.Current.CancellationToken);
                });

            // Act
            var handlerResult = await saveHandler.Save(state, TestContext.Current.CancellationToken);

            // Assert
            await _clientMediator.Received().Send(Arg.Is<SetCrossInstanceConfiguration>(a => a.CorrelationId == _correlationId), Arg.Any<CancellationToken>());

            Assert.True(state.ShowLanguageSavedBanner);
            Assert.IsType<SaveErrorResult>(handlerResult);
            Assert.Equal(errorMessage, handlerResult.Message);
            Assert.Equal(CrossInstanceConfigurationError.AddOrUpdateFailed, ((SaveErrorResult)handlerResult).ErrorCode);
        }
    }
}
