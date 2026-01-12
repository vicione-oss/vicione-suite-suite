using Blazor.Shared.Wizard.Services;
using SettingsConstants = Blazor.Shared.Settings.DateAndTime.Constants;

namespace Blazor.Shared.Onboarding.Services;

public sealed class TimeZoneWizardPageState : WizardPageState
{
    internal string SelectedTimeZoneId { get; set; } = SettingsConstants.DefaultTimeZoneId;
}
