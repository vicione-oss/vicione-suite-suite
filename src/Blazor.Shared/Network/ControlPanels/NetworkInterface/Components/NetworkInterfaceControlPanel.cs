using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;

/// <summary>
/// The parent for child network interface control panels with no content of its own.
/// </summary>
public sealed class NetworkInterfaceControlPanel : ControlPanelBase<NetworkInterfaceControlPanelState>;
