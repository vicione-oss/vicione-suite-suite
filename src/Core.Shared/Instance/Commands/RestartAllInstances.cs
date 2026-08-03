using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

/// <summary>
/// Global command directed at the master to restart every instance in the cluster. The master fans it out
/// as an instance-dependent <c>ControlInstance</c> restart command to each registered node. In a standalone
/// setup this restarts the single local instance.
/// </summary>
public sealed record RestartAllInstances : ICommand
{
    /// <summary>
    /// Delay each node waits before restarting, giving the UI time to inform the user.
    /// </summary>
    public TimeSpan? Delay { get; init; }

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
