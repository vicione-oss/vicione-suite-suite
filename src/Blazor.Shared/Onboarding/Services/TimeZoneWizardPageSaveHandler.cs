using Blazor.Shared.Settings.DateAndTime.Services;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Onboarding.Services;

internal sealed partial class TimeZoneWizardPageSaveHandler(ITimeZoneDescriptorProvider timeZoneDescriptorProvider,
    ITargetConfigurationProvider targetConfigurationProvider)
        : IWizardPageSaveHandler<TimeZoneWizardPageState>
{
    public async Task<ISaveResult> Save(TimeZoneWizardPageState state, CancellationToken cancellationToken)
    {
        var timeZoneDescriptor = await timeZoneDescriptorProvider.GetTimeZoneDescriptor(state.SelectedTimeZoneId, cancellationToken);
        if (timeZoneDescriptor is null)
            return new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneDescriptor);

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneDescriptor.Value.TimeZoneId, out var timeZoneInfo))
            return new SaveErrorResult(Settings.DateAndTime.Localization.ErrorMessages.CannotFindTimeZoneInfo);

        var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);
        targetConfiguration.TimeZone = timeZoneInfo;

        return new SaveSuccessResult();
    }
}
