using Blazor.Shared.Settings.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Extensions;

internal static class IControlPanelGroupDescriptorExtensions
{
    public static SettingsGroup ToSettingsGroup(this IControlPanelGroupDescriptor descriptor)
        => new() { Position = descriptor.Position };
}
