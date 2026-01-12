using HostManagement.Shared.Contracts;
using Sdk.Messaging;

namespace Core.Shared.HostManagement;

public sealed class GetHostMgmtSystemConfigurationResponse : IResponse
{
    public SystemConfiguration? Configuration { get; init; }
    public ErrorInfo? RequestError { get; init; }
}
