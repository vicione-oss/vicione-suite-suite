using Core.Shared.Instance.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

internal sealed class LanguageControlPanelResetHandler(IUiMediator mediator) : IControlPanelResetHandler<LanguageControlPanelState>
{
    public async Task Reset(LanguageControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            state.ShowPageRefreshInformation = false;
            state.ShowLanguageDoesNotAffectCurrentUser = false;

            var response = await mediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(new(), cancellationToken);
            state.CrossInstanceConfiguration = response.CrossInstanceConfiguration;

            state.SelectedCulture = Constants.SupportedCultures.FirstOrDefault(c => c.Name.Equals(state.CrossInstanceConfiguration?.CultureName, StringComparison.OrdinalIgnoreCase)) ?? LanguageControlPanelState.SelectedCultureDefault;
        }
        finally
        {
            state.EndLoading();
        }
    }
}
