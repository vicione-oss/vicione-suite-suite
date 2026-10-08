using HostManagement.Shared.Capabilities;

namespace Core.Shared.HostManagement.Requests;

/// <summary>
/// The HostManagement topics the UI checks before it restarts or shuts down the device or restarts the Suite.
/// </summary>
public sealed record SystemControlCapabilities(CapabilityStatus RestartSystem, CapabilityStatus RestartService, CapabilityStatus ShutdownSystem);
