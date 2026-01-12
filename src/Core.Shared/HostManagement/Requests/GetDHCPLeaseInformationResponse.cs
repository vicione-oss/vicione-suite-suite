using HostManagement.Shared.Contracts.Network;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public class GetDHCPLeaseInformationResponse : IResponse
{
    public DHCPLease? DHCPLease { get; set; }

    public ErrorInfo? RequestError { get; init; }
}
