using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public class GetSystemControlCapabilitiesResponse : IResponse
{
    /// <summary>
    /// <see langword="null"/> when HostManagement could not report its capabilities.
    /// </summary>
    public SystemControlCapabilities? Capabilities { get; init; }

    public ErrorInfo? RequestError { get; init; }
}
