using System.Diagnostics.CodeAnalysis;
using Blazor.Shared.Network.Extensions;
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
        // Sanitize
        state.Dns.Details.RemoveEmptyAndDuplicateItems();

        // Validate
        if (!ValidateNetworkInterfaceConfiguration(state.LocalNetwork, out var errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!ValidateNetworkInterfaceConfiguration(state.InternetConnection, out errorMessage))
            return new SaveErrorResult(errorMessage);

        if (!ValidateDnsConfiguration(state.Dns, out errorMessage))
            return new SaveErrorResult(errorMessage);

        // Apply
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
            proposedSystemConfiguration.UpdateFrom(state.Dns);

            var validateResult = await new SystemConfigurationValidator().ValidateAsync(proposedSystemConfiguration, cancellationToken);
            if (!validateResult.IsValid)
                return new SaveErrorResult(string.Join(" ", validateResult.Entries.Select(e => e.Message)));

            var targetConfiguration = await targetConfigurationProvider.GetTargetConfiguration(cancellationToken);
            state.LocalNetwork.ApplyTo(targetConfiguration.LocalNetwork);
            state.InternetConnection.ApplyTo(targetConfiguration.InternetConnection);
            state.Dns.ApplyTo(targetConfiguration.Dns);
        }
        finally
        {
            state.Dns.Details.EnsureAtLeastOneItemExists();

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
        }

        errorMessage = null;

        return true;
    }

    private bool ValidateDnsConfiguration(IDnsConfiguration dnsConfiguration, [MaybeNullWhen(true)] out string errorMessage)
    {
        var field = TechnicalTerms.DnsServer;

        foreach (var detail in dnsConfiguration.Details)
        {
            if (!requiredValidator.Validate(detail.IpAddress, field, out errorMessage))
                return false;
        }

        errorMessage = null;

        return true;
    }
}
