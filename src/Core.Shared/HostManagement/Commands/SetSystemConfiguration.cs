using HostManagement.Shared.Contracts;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Commands;

public record SetSystemConfiguration(SystemConfiguration SystemConfiguration) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
