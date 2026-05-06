using Blazor.Shared.Authorization.Extensions;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Core.Shared.UserManagement.Requests;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

internal sealed class LanguageControlPanelSaveHandler : ControlPanelSaveHandlerBase<LanguageControlPanelState>,
    IEventConsumer<CrossInstanceConfigurationChanged>,
    IEventConsumer<CrossInstanceConfigurationError>
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public LanguageControlPanelSaveHandler(IUiMediator mediator, AuthenticationStateProvider authenticationStateProvider) : base(mediator)
    {
        _authenticationStateProvider = authenticationStateProvider;

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
        if (saveResult is SaveErrorResult)
            return saveResult;

        // If the user has a language preference he's not affected by default language change
        var userLanguage = await GetUserLanguage(cancellationToken);
        if (!string.IsNullOrEmpty(userLanguage))
        {
            state.ShowLanguageDoesNotAffectCurrentUser = true;
            return saveResult;
        }

        // User has no language preference, so we show the banner that informs them that the
        // language has been changed and they need to refresh the page to see the change
        state.ShowPageRefreshInformation = true;

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

    private async Task<string?> GetUserLanguage(CancellationToken cancellationToken = default)
    {
        var userName = await _authenticationStateProvider.GetUserName();
        if (string.IsNullOrEmpty(userName))
            return null;

        var response = await Mediator.Request<GetUsers, GetUsersResponse>(new GetUsers(new(userName)), cancellationToken);
        return response.Users.FirstOrDefault()?.Language;
    }
}
