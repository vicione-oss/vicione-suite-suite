using HostManagement.Shared.Contracts.Network;
using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public sealed record RenewDHCPLeaseResponse : IResponse
{
    public DHCPLease? DHCPLease { get; set; }

    public ErrorInfo? RequestError { get; init; }
}
