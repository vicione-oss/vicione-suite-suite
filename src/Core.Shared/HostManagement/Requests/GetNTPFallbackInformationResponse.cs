using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public class GetNTPFallbackInformationResponse : IResponse
{
    public List<string>? FallbackNTPServers { get; set; }

    public ErrorInfo? RequestError { get; init; }
}

