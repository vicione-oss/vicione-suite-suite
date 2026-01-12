using Sdk.Messaging;

namespace Core.Shared.HostManagement;

public record InstallSuiteVersion(string PackageName, string SignatureName) : ICommand
{
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
