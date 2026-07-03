using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Core.Shared.HostManagement;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Onboarding.Services;

internal sealed class TargetConfigurationProvider(IUiMediator mediator, [FromKeyedServices(Sdk.Constants.ClientTimeProviderServiceKey)] TimeProvider timeProvider)
    : ITargetConfigurationProvider
{
    private TargetConfiguration? _targetConfiguration;

    public async Task<ITargetConfiguration> GetTargetConfiguration(CancellationToken cancellationToken = default)
        => _targetConfiguration ??= await CreateTargetConfiguration(cancellationToken);

    private async Task<TargetConfiguration> CreateTargetConfiguration(CancellationToken cancellationToken)
    {
        var result = new TargetConfiguration();

        var request = new GetHostMgmtSystemConfiguration();
        var response = await mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(request, cancellationToken);

        var systemConfiguration = response.Configuration;
        if (systemConfiguration is not null)
        {
            result.Hostname = systemConfiguration.NetworkDNSSettings.Hostname;

            result.LocalNetwork.UpdateFrom(systemConfiguration);
            result.InternetConnection.UpdateFrom(systemConfiguration);

            result.Dns.UpdateFrom(systemConfiguration.NetworkDNSSettings);
        }

        result.TimeZone = timeProvider.LocalTimeZone;

        return result;
    }
}
