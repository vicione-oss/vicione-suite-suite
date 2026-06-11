using Blazor.Shared.Settings.DateAndTime.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;

internal sealed class DateAndTimeControlPanelSaveHandler(ITimeZoneDescriptorProvider timeZoneDescriptorProvider, IUiMediator mediator) : UserInterfaceControlPanelSaveHandlerBase<DateAndTimeControlPanelState>(mediator)
{
    protected override async Task<UserInterfaceSaveResult> SaveInternal(DateAndTimeControlPanelState state, CancellationToken cancellationToken)
    {
        var result = new UserInterfaceSaveResult();

        var timeZoneDescriptor = await timeZoneDescriptorProvider.GetTimeZoneDescriptor(state.SelectedTimeZoneId, cancellationToken);

        if (timeZoneDescriptor is null)
        {
            result.ErrorSaveResult = new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneDescriptor);
            return result;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneDescriptor.Value.TimeZoneId, out _))
        {
            result.ErrorSaveResult = new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneInfo);
            return result;
        }

        result.TimeZoneId = state.SelectedTimeZoneId;
        return result;
    }
}
