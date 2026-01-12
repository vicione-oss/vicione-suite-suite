using Riok.Mapperly.Abstractions;
using Sdk.NetworkStatus.Contracts;
using Hm = HostManagement.Shared;

namespace Core.OS.HostManagement.Mappers;

[Mapper]
public partial class NetworkStatusInformationMapper
{
    public partial NetworkStatusInformation? ToSuiteFormat(Hm.Contracts.NetworkStatus.NetworkStatusInformation? hostManagementNetworkStatusInformation);
}
