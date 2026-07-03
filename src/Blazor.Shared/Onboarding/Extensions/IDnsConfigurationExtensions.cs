using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Onboarding.Models;
using HostManagement.Shared.Contracts.Network;
using Riok.Mapperly.Abstractions;

namespace Blazor.Shared.Onboarding.Extensions;

[Mapper(UseDeepCloning = true)]
internal static partial class IDnsConfigurationExtensions
{
    public static partial void ApplyTo(this IDnsConfiguration target, IDnsConfiguration source);
}

internal static partial class IDnsConfigurationExtensions
{
    public static void UpdateFrom(this IDnsConfiguration target, NetworkDNSSettings source)
    {
        target.Enabled = source.NameServersEnabled;

        target.Details.UpdateFrom(source.NameServers);
    }
}
