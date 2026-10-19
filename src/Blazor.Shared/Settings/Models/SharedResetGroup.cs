using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Models;

/// <summary>
/// The owner and handler by which edits of different control panels reset their entity together, taken from a
/// <see cref="ISharedControlPanelHandler"/>; equal for all edits sharing it.
/// </summary>
internal readonly record struct SharedResetGroup(Type OwnerServiceKey, Type HandlerType);
