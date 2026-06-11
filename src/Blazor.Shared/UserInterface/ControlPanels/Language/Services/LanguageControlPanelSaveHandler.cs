using Blazor.Shared.Authorization.Extensions;
using Core.Shared.UserManagement.Requests;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

internal sealed class LanguageControlPanelSaveHandler(IUiMediator mediator, AuthenticationStateProvider authenticationStateProvider)
    : UserInterfaceControlPanelSaveHandlerBase<LanguageControlPanelState>(mediator)
{
    protected override async Task AfterSaveInternal(LanguageControlPanelState state, ISaveResult? saveResult, CancellationToken cancellationToken)
    {
        if (saveResult is SaveErrorResult)
            return;

        // If the user has a language preference he's not affected by default language change
        var userLanguage = await GetUserLanguage(cancellationToken);

        if (!string.IsNullOrEmpty(userLanguage))
        {
            state.ShowLanguageDoesNotAffectCurrentUser = true;
            return;
        }

        // User has no language preference, so we show the banner that informs them that the
        // language has been changed and they need to refresh the page to see the change
        state.ShowPageRefreshInformation = true;
    }

    protected override async Task<UserInterfaceSaveResult> SaveInternal(LanguageControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.CrossInstanceConfiguration is null)
            throw new InvalidOperationException(Localization.LanguageControlPanelSaveHandler.NoApplicationConfigurationFound);

        state.CrossInstanceConfiguration.CultureName = state.SelectedCulture.Name;

        return new UserInterfaceSaveResult()
        {
            CultureName = state.CrossInstanceConfiguration.CultureName,
            TimeZoneId = state.CrossInstanceConfiguration.TimeZoneId,
        };
    }

    private async Task<string?> GetUserLanguage(CancellationToken cancellationToken = default)
    {
        var userName = await authenticationStateProvider.GetUserName();
        if (string.IsNullOrEmpty(userName))
            return null;

        var response = await Mediator.Request<GetUsers, GetUsersResponse>(new GetUsers(new(userName)), cancellationToken);
        return response.Users.FirstOrDefault()?.Language;
    }
}
