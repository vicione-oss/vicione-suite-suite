using Blazor.Shared.Onboarding.Models;

namespace Blazor.Shared.Onboarding.Services;

internal interface ITargetConfigurationProvider
{
    Task<ITargetConfiguration> GetTargetConfiguration(CancellationToken cancellationToken = default);
}
