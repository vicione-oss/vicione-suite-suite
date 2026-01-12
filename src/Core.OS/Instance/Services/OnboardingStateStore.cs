using Core.OS.DbContext;
using Core.OS.Instance.Mappers;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Services;
using Microsoft.EntityFrameworkCore;

namespace Core.OS.Instance.Services;

internal sealed class OnboardingStateStore(IApplicationDbContext dbContext) : IOnboardingStateStore, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1);

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _semaphore.Dispose();

        _disposed = true;
    }

    public IOnboardingState GetOnboardingState(Guid instanceId)
    {
        var onboardingState = dbContext.OnboardingStates.Where(i => i.InstanceId == instanceId).SingleOrDefault()
            ?? new OnboardingState { InstanceId = instanceId };

        return onboardingState;
    }

    public async Task<IOnboardingState> GetOnboardingStateAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var onboardingState = await dbContext.OnboardingStates.Where(i => i.InstanceId == instanceId).SingleOrDefaultAsync(cancellationToken)
            ?? new OnboardingState { InstanceId = instanceId };

        return onboardingState;
    }

    public async Task SetOnboardingStateAsync(IOnboardingState onboardingState, CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                var targetOnboardingState = await dbContext.OnboardingStates.FindAsync([onboardingState.InstanceId], cancellationToken);

                if (targetOnboardingState is null)
                {
                    targetOnboardingState = new OnboardingState { InstanceId = onboardingState.InstanceId };

                    dbContext.OnboardingStates.Add(targetOnboardingState);
                }

                onboardingState.MapTo(targetOnboardingState);

                await dbContext.Instance.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore already disposed, nothing we can do, return gracefully
        }
    }
}
