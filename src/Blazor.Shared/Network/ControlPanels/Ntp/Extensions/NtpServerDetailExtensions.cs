using Blazor.Shared.Network.ControlPanels.Ntp.Models;
using Blazor.Shared.Network.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Extensions;

internal static class NtpServerDetailExtensions
{
    public static void Save(this List<NtpServerDetail> nptServerDetails, List<string>? ntpServers)
    {
        // filter out fieldsets not filled and remove duplicates
        var filteredNptServerDetails = nptServerDetails.Where(d => !string.IsNullOrWhiteSpace(d.IpAddressOrHostname))
            .Distinct()
            .ToList();

        (ntpServers ??= []).Clear();
        ntpServers.AddRange(filteredNptServerDetails.Select(d => d.IpAddressOrHostname));

        nptServerDetails.Clear();
        nptServerDetails.AddRange(filteredNptServerDetails);
        nptServerDetails.EnsureAtLeastOneItemExists();
    }

    public static void Reset(this List<NtpServerDetail> nptServerDetails, List<string>? ntpServers)
    {
        nptServerDetails.Clear();

        foreach (var nptServer in (ntpServers ?? []).Select(d => new NtpServerDetail { IpAddressOrHostname = d }).Distinct())
            nptServerDetails.Add(nptServer);

        nptServerDetails.EnsureAtLeastOneItemExists();
    }
}
