using Sdk.Messaging;

namespace Core.Shared.HostManagement;

public record GetAvailableSuiteVersionsResponse(List<string> Versions) : IResponse
{
    public ErrorInfo? RequestError { get; set; }
}
