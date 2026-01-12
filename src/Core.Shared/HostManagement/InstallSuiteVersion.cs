using Sdk.Messaging;

namespace Core.Shared.HostManagement;

public record InstallSuiteVersion(string Version) : ICommand
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
