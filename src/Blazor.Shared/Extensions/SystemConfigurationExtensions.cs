using System.Net;
using HostManagement.Shared.Contracts;
using Riok.Mapperly.Abstractions;

namespace Blazor.Shared.Extensions;

[Mapper(UseDeepCloning = true)]
public static partial class SystemConfigurationExtensions
{
    [MapperIgnoreSource(nameof(SystemConfiguration.Version))]
    [MapperIgnoreSource(nameof(SystemConfiguration.Services))] // we need to map this ourselfs because property has no setter causing Mapperly to generate a foreach that fails when source.Services and target.Services is the same instance
    private static partial void ApplyToInternal(this SystemConfiguration source, SystemConfiguration target);

    private static IPAddress IPAddressToIPAddress(IPAddress ipAddress) => IPAddress.Parse(ipAddress.ToString());
}

public static partial class SystemConfigurationExtensions
{
    public static void ApplyTo(this SystemConfiguration source, SystemConfiguration target)
    {
        source.ApplyToInternal(target);

        var services = source.Services == target.Services ? [.. source.Services] : source.Services;

        target.Services.Clear();
        target.Services.AddRange(services);
    }
}
