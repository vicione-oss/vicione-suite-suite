using Riok.Mapperly.Abstractions;
using Sdk.SystemConfiguration.Contracts;
using Hm = HostManagement.Shared;

namespace Core.OS.HostManagement.Mappers;

[Mapper(IgnoreObsoleteMembersStrategy = IgnoreObsoleteMembersStrategy.Both)]
public partial class SystemConfigurationMapper
{
    public partial SystemConfiguration ToSuiteFormat(Hm.Contracts.SystemConfiguration? hostManagementSysConfig);

    public partial Hm.Contracts.SystemConfiguration ToHostManagementFormat(SystemConfiguration? suiteSysConfig);
}
