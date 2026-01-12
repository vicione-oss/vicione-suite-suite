using Core.Shared.Instance.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;

internal sealed class DateAndTimeControlPanelResetHandler(IUiMediator mediator)
    : IControlPanelResetHandler<DateAndTimeControlPanelState>
{
    public async Task Reset(DateAndTimeControlPanelState state, CancellationToken cancellationToken)
    {
        var response = await mediator.Request<GetCrossInstanceConfiguration, GetCrossInstanceConfigurationResponse>(new(), cancellationToken);
        state.SelectedTimeZoneId = response.CrossInstanceConfiguration.TimeZoneId;
    }
}
