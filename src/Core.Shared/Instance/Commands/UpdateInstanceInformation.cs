using Sdk.Instance;
using Sdk.Messaging;

namespace Core.Shared.Instance.Commands;

public sealed record UpdateInstanceInformation(IInstanceInformation InstanceInformation) : ICommand
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
