using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Onboarding.Services;

internal sealed partial class HostnameWizardPageResetHandler(ITargetConfigurationProvider targetConfigurationProvider)
    : IWizardPageResetHandler<HostnameWizardPageState>
{
    public async Task Reset(HostnameWizardPageState state, CancellationToken cancellationToken)
    {
        state.BeginOperation(new WizardOperation { Description = Localization.CommonWizardOperations.PrepopulatingFields, EstimatedDurationMs = 3000 });
        try
        {
            var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);

            state.Hostname = targetConfiguration.Hostname;
        }
        finally
        {
            state.EndOperation();
        }
    }
}
