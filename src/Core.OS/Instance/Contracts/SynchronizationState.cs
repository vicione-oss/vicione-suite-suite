using MassTransit.Transports;

namespace Core.OS.Instance.Contracts;

public sealed class SynchronizationState : IReceiveEndpointDependency
{
    private readonly TaskCompletionSource _tcs = new();

    public bool SynchronizationSucceeded => _tcs.Task.IsCompleted;
    public Task Ready => _tcs.Task;

    public void CompleteSynchronization() => _tcs.SetResult();
}
