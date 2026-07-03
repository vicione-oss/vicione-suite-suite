using System.Net;
using Blazor.Shared.Network.ControlPanels.Dns.Models;

namespace Blazor.Shared.Network.Extensions;

internal static class NetworkInterfaceDnsDetailExtensions
{
    public static void RemoveEmptyAndDuplicateItems(this List<NetworkInterfaceDnsDetail> list)
    {
        if (list.Count == 0)
            return;

        var uniqueDnsDetails = list.Where(d => !string.IsNullOrWhiteSpace(d.IpAddress)).Distinct().ToList();

        list.Clear();
        list.AddRange(uniqueDnsDetails);
    }

    public static void UpdateFrom(this List<NetworkInterfaceDnsDetail> list, IReadOnlyList<IPAddress> source)
    {
        list.Clear();

        if (source.Count > 0)
        {
            var dnsDetails = source.Select(d => new NetworkInterfaceDnsDetail { IpAddress = d.ToString() })
                .Distinct();

            list.AddRange(dnsDetails);
        }
    }
}
