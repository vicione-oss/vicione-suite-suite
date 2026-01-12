using Core.Shared.Instance.Contracts;

namespace Core.Shared.Instance.Services;

/// <summary>
/// Abstraction for a store which manages <see cref="OnboardingState"/>
/// </summary>
public interface IOnboardingStateStore
{
    IOnboardingState GetOnboardingState(Guid instanceId);
    Task<IOnboardingState> GetOnboardingStateAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task SetOnboardingStateAsync(IOnboardingState onboardingState, CancellationToken cancellationToken = default);
}
