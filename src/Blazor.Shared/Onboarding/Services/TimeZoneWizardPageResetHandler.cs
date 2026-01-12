using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Onboarding.Services;

internal sealed partial class TimeZoneWizardPageResetHandler(ITargetConfigurationProvider targetConfigurationProvider)
    : IWizardPageResetHandler<TimeZoneWizardPageState>
{
    public async Task Reset(TimeZoneWizardPageState state, CancellationToken cancellationToken)
    {
        state.BeginOperation(new WizardOperation { Description = Localization.CommonWizardOperations.PrepopulatingFields, EstimatedDurationMs = 3000 });
        try
        {
            var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);

            state.SelectedTimeZoneId = targetConfiguration.TimeZone.Id;
        }
        finally
        {
            state.EndOperation();
        }
    }
}
