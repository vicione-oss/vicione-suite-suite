using MassTransit.Transports;

namespace Core.OS.Instance.Services;

public sealed class SynchronizationState : IReceiveEndpointDependency
{
    private readonly Lock _lock = new();
    private TaskCompletionSource _tcs = new();

    public bool SynchronizationSucceeded => _tcs.Task.IsCompleted;
    public Task Ready => _tcs.Task;

    public void CompleteSynchronization() => _tcs.TrySetResult();

    public void Reset()
    {
        lock (_lock)
        {
            if (!_tcs.Task.IsCompleted)
                return;

            _tcs = new TaskCompletionSource();
        }
    }
}
