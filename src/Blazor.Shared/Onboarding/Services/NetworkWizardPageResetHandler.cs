using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Wizard.Models;
using Blazor.Shared.Wizard.Services;

namespace Blazor.Shared.Onboarding.Services;

internal sealed partial class NetworkWizardPageResetHandler(ITargetConfigurationProvider targetConfigurationProvider)
    : IWizardPageResetHandler<NetworkWizardPageState>
{
    public async Task Reset(NetworkWizardPageState state, CancellationToken cancellationToken)
    {
        state.BeginOperation(new WizardOperation { Description = Localization.CommonWizardOperations.PrepopulatingFields, EstimatedDurationMs = 3000 });
        try
        {
            var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);

            targetConfiguration.LocalNetwork.ApplyTo(state.LocalNetwork);
            targetConfiguration.InternetConnection.ApplyTo(state.InternetConnection);
        }
        finally
        {
            state.EndOperation();
        }
    }
}
