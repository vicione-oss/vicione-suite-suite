using System.Net;
using IPNetwork = System.Net.IPNetwork;

namespace Core.OS.Instance;

internal sealed record TrustedProxySafelist(
    IReadOnlyList<IPAddress> Proxies,
    IReadOnlyList<IPNetwork> Networks)
{
    public static TrustedProxySafelist Parse(IReadOnlyList<string> entries)
    {
        var proxies = new List<IPAddress>();
        var networks = new List<IPNetwork>();

        foreach (var entry in entries)
        {
            if (IPAddress.TryParse(entry, out var address))
                proxies.Add(address);
            else if (IPNetwork.TryParse(entry, out var network))
                networks.Add(network);
            else
            {
                throw new InvalidOperationException(
                    $"{InstanceOptions.ConfigSection}:{nameof(InstanceOptions.TrustedProxies)} entry '{entry}' is neither "
                    + "an IP address (e.g. \"10.0.0.5\") nor a CIDR network (e.g. \"10.0.0.0/24\").");
            }
        }

        return new TrustedProxySafelist(proxies, networks);
    }
}
