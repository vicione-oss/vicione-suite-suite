using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public class GetOriginalPhysicalAddressResponse : IResponse
{
    public string? OriginalPhysicalAddress { get; set; }

    public ErrorInfo? RequestError { get; init; }
}
