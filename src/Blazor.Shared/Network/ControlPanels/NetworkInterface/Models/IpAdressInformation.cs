using System.Net;
using Blazor.Shared.Settings.NetworkInterface.Enums;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;

internal record IpAdressInformation(IpConfigurationMode Configuration, IPAddress Address, IPAddress Subnet, IPAddress? Gateway);
