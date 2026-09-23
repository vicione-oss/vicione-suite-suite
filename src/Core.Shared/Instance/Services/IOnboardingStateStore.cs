using Core.Shared.Instance.Contracts;

namespace Core.Shared.Instance.Services;

public interface IOnboardingStateStore
{
    IOnboardingState GetOnboardingState(Guid instanceId);
    Task<IOnboardingState> GetOnboardingStateAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task SetOnboardingStateAsync(IOnboardingState onboardingState, CancellationToken cancellationToken = default);
}
