using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public class GetNTPFallbackInformationResponse : IResponse
{
    public List<string>? FallbackNTPServers { get; init; }

    public ErrorInfo? RequestError { get; init; }
}

