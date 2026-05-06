using Blazor.Shared.Settings.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Extensions;

internal static class IControlPanelCategoryDescriptorExtensions
{
    public static SettingsCategory ToSettingsCategory(this IControlPanelCategoryDescriptor descriptor, int groupPosition)
        => new()
        {
            Title = descriptor.Title,
            IconCssClass = descriptor.IconCssClass,
            IconUrl = descriptor.IconUrl,
            Position = descriptor.Position,
            GroupPosition = groupPosition
        };
}
