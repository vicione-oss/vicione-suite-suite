using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public record RenewDHCPLease(string NetworkInterfaceName) : IRequest<RenewDHCPLeaseResponse>;
