namespace Blazor.Shared.SystemInformation.Models;

/// <summary>
/// A socket of a switch built into the device, such as <c>lan1</c> on an edge device.
/// </summary>
/// <param name="Name">The socket name, such as <c>lan1</c>.</param>
/// <param name="Connected">Whether a cable is plugged in.</param>
/// <param name="SpeedMbit"><c>null</c> without a cable.</param>
public sealed record NetworkPort(string Name, bool Connected, double? SpeedMbit);
