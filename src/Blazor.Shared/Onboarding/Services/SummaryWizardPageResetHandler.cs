using Blazor.Shared.Onboarding.Extensions;
using Core.Shared.HostManagement;
using Sdk.Client.Infrastructure;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;
using Sdk.Instance;
using ViciOne.Ui.Localization.Extensions;

namespace Blazor.Shared.Onboarding.Services;

internal sealed partial class SummaryWizardPageResetHandler(ITargetConfigurationProvider targetConfigurationProvider,
    IInstanceInformationProvider instanceInformationProvider, IUiMediator mediator)
        : IWizardPageResetHandler<SummaryWizardPageState>
{
    public async Task Reset(SummaryWizardPageState state, CancellationToken cancellationToken)
    {
        state.BeginOperation(new WizardOperation { Description = Localization.CommonWizardOperations.PrepopulatingFields, EstimatedDurationMs = 3000 });
        try
        {
            var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);

            state.Hostname = targetConfiguration.Hostname;
            state.SerialNumber = instanceInformationProvider.Local.SerialNumber;

            state.TimeZone = targetConfiguration.TimeZone.DisplayName;

            var localizedDateTimeOffset = DateTimeOffset.UtcNow.AdjustToTimeZone(targetConfiguration.TimeZone);
            state.Date = localizedDateTimeOffset.LocalizeShortDate();
            state.Time = localizedDateTimeOffset.LocalizeShortTime();

            targetConfiguration.LocalNetwork.ApplyTo(state.LocalNetwork);
            targetConfiguration.InternetConnection.ApplyTo(state.InternetConnection);
            targetConfiguration.Dns.ApplyTo(state.Dns);

            state.BeginOperation(new WizardOperation { Description = Localization.SummaryWizardPageResetHandler.DetectingAutomaticRestart, EstimatedDurationMs = 3000 });
            try
            {
                var request = new GetHostMgmtSystemConfiguration();
                var response = await mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(request, cancellationToken);

                var currentSystemConfiguration = response.Configuration
                    ?? throw new InvalidOperationException("Retrieving current system configuration failed");

                var proposedSystemConfiguration = currentSystemConfiguration.Clone();

                proposedSystemConfiguration.UpdateFrom(targetConfiguration.LocalNetwork);
                proposedSystemConfiguration.UpdateFrom(targetConfiguration.InternetConnection);
                proposedSystemConfiguration.UpdateFrom(targetConfiguration.Dns);

                proposedSystemConfiguration.NetworkDNSSettings.Hostname = targetConfiguration.Hostname;

                if (!currentSystemConfiguration.Equals(proposedSystemConfiguration))
                    state.NewSystemConfiguration = proposedSystemConfiguration;
                else
                    state.NewSystemConfiguration = null;
            }
            finally
            {
                state.EndOperation();
            }
        }
        finally
        {
            state.EndOperation();
        }
    }
}
