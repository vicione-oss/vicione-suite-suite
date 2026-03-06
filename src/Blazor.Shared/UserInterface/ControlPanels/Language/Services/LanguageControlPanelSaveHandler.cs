using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

internal sealed class LanguageControlPanelSaveHandler : ControlPanelSaveHandlerBase<LanguageControlPanelState>,
    IEventConsumer<CrossInstanceConfigurationChanged>,
    IEventConsumer<CrossInstanceConfigurationError>
{
    public LanguageControlPanelSaveHandler(IUiMediator mediator) : base(mediator)
    {
        Register<CrossInstanceConfigurationChanged>();
        Register<CrossInstanceConfigurationError>();
    }

    public override async Task<ISaveResult> Save(LanguageControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.CrossInstanceConfiguration is null)
            throw new InvalidOperationException(Localization.LanguageControlPanelSaveHandler.NoApplicationConfigurationFound);

        state.CrossInstanceConfiguration.CultureName = state.SelectedCulture.Name;

        var command = new SetCrossInstanceConfiguration(state.CrossInstanceConfiguration.CultureName, state.CrossInstanceConfiguration.TimeZoneId);

        var saveResult = await SendAndWaitForCompletion(command, cancellationToken);
        state.ShowLanguageSavedBanner = true;
        return saveResult;
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationChanged> context, CancellationToken cancellationToken)
    {
        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationError> context, CancellationToken cancellationToken = default)
    {
        CompleteWithError(context.Message.CorrelationId, context.Message.Error);

        return Task.CompletedTask;
    }
}
