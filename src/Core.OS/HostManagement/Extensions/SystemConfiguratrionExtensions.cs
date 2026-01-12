using System.Net;
using HostManagement.Shared.Contracts;
using Riok.Mapperly.Abstractions;

namespace Core.OS.HostManagement.Extensions;

[Mapper(UseDeepCloning = true)]
internal static partial class SystemConfiguratrionExtensions
{
    [MapperIgnoreSource(nameof(SystemConfiguration.Version))]
    [MapperIgnoreSource(nameof(SystemConfiguration.Services))] // we need to map this ourselfs because property has no setter causing Mapperly to generate a foreach that fails when source.Services and target.Services is the same instance
    private static partial void ApplyToInternal(this SystemConfiguration source, SystemConfiguration target);

    private static IPAddress IPAddressToIPAddress(IPAddress ipAddress) => IPAddress.Parse(ipAddress.ToString());
}

internal static partial class SystemConfiguratrionExtensions
{
    public static void ApplyTo(this SystemConfiguration source, SystemConfiguration target)
    {
        source.ApplyToInternal(target);

        var services = source.Services == target.Services ? [.. source.Services] : source.Services;

        target.Services.Clear();
        target.Services.AddRange(services);
    }
}
