using System.Diagnostics.CodeAnalysis;
using Blazor.Shared.Network.Services.Validators;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Blazor.Shared.Validation.Services.Validators;
using Core.Shared.HostManagement;
using HostManagement.Shared.Validation;
using Sdk.Client.Infrastructure;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class NetworkWizardPageSaveHandler(IUiMediator mediator, IRequiredValidator requiredValidator,
    IIpAddressValidator ipAddressValidator, ITargetConfigurationProvider targetConfigurationProvider)
        : IWizardPageSaveHandler<NetworkWizardPageState>
{
    public async Task<ISaveResult> Save(NetworkWizardPageState state, CancellationToken cancellationToken)
    {
        if (!ValidateNetworkInterfaceConfiguration(state.LocalNetwork, out var errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!ValidateNetworkInterfaceConfiguration(state.InternetConnection, out errorMessage))
            return new SaveErrorResult(errorMessage);

        state.BeginOperation(new WizardOperation { Description = Localization.NetworkWizardPageSaveHandler.ValidatingAggregatedSystemConfiguration, EstimatedDurationMs = 3000 });
        try
        {
            var request = new GetHostMgmtSystemConfiguration();
            var response = await mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(request, cancellationToken);

            var currentSystemConfiguration = response.Configuration;
            if (currentSystemConfiguration is null)
                return new SaveErrorResult(Localization.NetworkWizardPageSaveHandler.RetrievingCurrentSystemConfigurationFailed);

            var proposedSystemConfiguration = currentSystemConfiguration.Clone();

            proposedSystemConfiguration.UpdateFrom(state.LocalNetwork);
            proposedSystemConfiguration.UpdateFrom(state.InternetConnection);

            var validateResult = await new SystemConfigurationValidator().ValidateAsync(proposedSystemConfiguration, cancellationToken);
            if (!validateResult.IsValid)
                return new SaveErrorResult(string.Join(" ", validateResult.Entries.Select(e => e.Message)));

            var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);
            state.LocalNetwork.ApplyTo(targetConfiguration.LocalNetwork);
            state.InternetConnection.ApplyTo(targetConfiguration.InternetConnection);
        }
        finally
        {
            state.EndOperation();
        }

        return new SaveSuccessResult();
    }

    private bool ValidateNetworkInterfaceConfiguration(INetworkInterfaceConfiguration networkInterfaceConfiguration,
        [MaybeNullWhen(true)] out string errorMessage)
    {
        if (networkInterfaceConfiguration.ConfigurationMode == IpConfigurationMode.Manual)
        {
            var fieldPrefix = networkInterfaceConfiguration.GetFriendlyName();

            // IP address
            var field = $"{fieldPrefix} {TechnicalTerms.IpAddress}";

            if (!requiredValidator.Validate(networkInterfaceConfiguration.IpAddress, field, out errorMessage))
                return false;

            if (!ipAddressValidator.Validate(networkInterfaceConfiguration.IpAddress, field, out errorMessage))
                return false;

            // Subnet mask
            field = $"{fieldPrefix} {TechnicalTerms.SubnetMask}";

            if (!requiredValidator.Validate(networkInterfaceConfiguration.SubnetMask, field, out errorMessage))
                return false;

            if (!ipAddressValidator.Validate(networkInterfaceConfiguration.SubnetMask, field, out errorMessage))
                return false;

            // Default gateway
            if (!string.IsNullOrWhiteSpace(networkInterfaceConfiguration.DefaultGateway))
            {
                field = $"{fieldPrefix} {TechnicalTerms.DefaultGateway}";

                if (!ipAddressValidator.Validate(networkInterfaceConfiguration.DefaultGateway, field, out errorMessage))
                    return false;
            }

            // Dns server
            if (!string.IsNullOrWhiteSpace(networkInterfaceConfiguration.DnsServer))
            {
                field = $"{fieldPrefix} {TechnicalTerms.DnsServer}";

                if (!ipAddressValidator.Validate(networkInterfaceConfiguration.DnsServer, field, out errorMessage))
                    return false;
            }
        }

        errorMessage = null;

        return true;
    }
}
