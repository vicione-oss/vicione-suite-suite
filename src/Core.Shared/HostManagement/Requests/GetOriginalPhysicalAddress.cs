using Sdk.Messaging;

namespace Core.Shared.HostManagement.Requests;

public record GetOriginalPhysicalAddress(string NetworkInterfaceName) : IRequest<GetOriginalPhysicalAddressResponse>;

