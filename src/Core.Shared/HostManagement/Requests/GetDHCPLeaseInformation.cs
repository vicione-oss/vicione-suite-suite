using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public record GetDHCPLeaseInformation(string NetworkInterfaceName) : IRequest<GetDHCPLeaseInformationResponse>;

