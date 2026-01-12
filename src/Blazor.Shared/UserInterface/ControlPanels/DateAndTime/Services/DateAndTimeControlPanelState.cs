using Sdk.Client.ControlPanels.Services;
using SettingsConstants = Blazor.Shared.Settings.DateAndTime.Constants;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;

public sealed class DateAndTimeControlPanelState : ControlPanelState
{
    internal string SelectedTimeZoneId { get; set; } = SettingsConstants.DefaultTimeZoneId;
}
