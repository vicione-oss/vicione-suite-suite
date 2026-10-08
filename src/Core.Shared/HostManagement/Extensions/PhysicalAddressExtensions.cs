using System.Globalization;
using System.Net.NetworkInformation;

namespace Core.Shared.HostManagement.Extensions;

public static class PhysicalAddressExtensions
{
    extension(PhysicalAddress address)
    {
        /// <summary>
        /// Formats the address as colon separated hexadecimal octets (e.g. 00:02:01:10:53:25), the notation HostManagement uses.
        /// </summary>
        public string ToColonNotation()
            => string.Join(':', address.GetAddressBytes().Select(octet => octet.ToString("X2", CultureInfo.InvariantCulture)));
    }
}
