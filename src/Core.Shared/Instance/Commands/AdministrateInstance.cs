using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

public sealed class AdministrateInstance : ICommand
{
    public Guid InstanceId { get; set; }
    public AdministrateInstanceAction Action { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}

public enum AdministrateInstanceAction
{
    Synchronize,
    Delete
}
