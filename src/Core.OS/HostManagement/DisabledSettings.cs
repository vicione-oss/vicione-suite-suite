using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Contracts;

namespace Core.OS.HostManagement;

/// <summary>
/// Finds the disabled settings that a proposed configuration would change.
/// </summary>
/// <remarks>
/// Mirrors the internal HostManagement <c>SupportedSettingsValidator</c>, which rejects the whole configuration
/// when one of these settings changes. Interfaces and services are matched by name.
/// </remarks>
internal static class DisabledSettings
{
    public static IReadOnlyList<string> ChangedBy(SystemConfiguration current, SystemConfiguration proposed, SupportedSettings support)
        => GetChangedSettings(current, proposed, support)
            .Where(setting => setting.Capability is CapabilityStatus.Disabled)
            .Select(setting => setting.Name)
            .ToList();

    private static IEnumerable<LeafSettingSupport> GetChangedSettings(SystemConfiguration current, SystemConfiguration proposed, SupportedSettings support)
    {
        var interfaceSupport = support.NetworkInterfaces.NetworkInterfaceDetail;

        if (HasChanges(current.NetworkInterfaces.Select(i => (i.CommonInformation.Name, i.IPv4)),
                proposed.NetworkInterfaces.Select(i => (i.CommonInformation.Name, i.IPv4)), i => i.Name))
            yield return interfaceSupport.IPv4;

        if (HasChanges(current.NetworkInterfaces.Select(i => (i.CommonInformation.Name, i.VLAN)),
                proposed.NetworkInterfaces.Select(i => (i.CommonInformation.Name, i.VLAN)), i => i.Name))
            yield return interfaceSupport.VLAN;

        var currentDns = current.NetworkDNSSettings;
        var proposedDns = proposed.NetworkDNSSettings;

        if (HasChanges(currentDns.Hostname, proposedDns.Hostname))
            yield return support.DNS.Hostname;

        if (HasChanges(currentDns.MulticastDNSEnabled, proposedDns.MulticastDNSEnabled))
            yield return support.DNS.MulticastDNS;

        if (HasChanges(currentDns.StaticHosts, proposedDns.StaticHosts))
            yield return support.DNS.StaticHosts;

        if (HasChanges(currentDns.NameServers, proposedDns.NameServers))
            yield return support.DNS.NameServers;

        if (HasChanges(currentDns.PrimaryDNSSuffix, proposedDns.PrimaryDNSSuffix))
            yield return support.DNS.PrimaryDNSSuffix;

        if (HasChanges(currentDns.SearchDomains, proposedDns.SearchDomains))
            yield return support.DNS.SearchDomains;

        if (HasChanges(current.NetworkProxySettings, proposed.NetworkProxySettings))
            yield return support.Proxy;

        if (HasChanges(current.NetworkNTPSettings, proposed.NetworkNTPSettings))
            yield return support.NTP;

        if (HasChanges(current.Services, proposed.Services, service => service.Name))
            yield return support.Services;
    }

    private static bool HasChanges<T>(T current, T proposed)
        => !EqualityComparer<T>.Default.Equals(current, proposed);

    private static bool HasChanges<T>(IEnumerable<T> current, IEnumerable<T> proposed, Func<T, string> name)
        => !current.OrderBy(name).SequenceEqual(proposed.OrderBy(name));
}
