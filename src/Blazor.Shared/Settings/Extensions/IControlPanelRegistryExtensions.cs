using Blazor.Shared.Settings.Comparers;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Extensions;

internal static class IControlPanelRegistryExtensions
{
    /// <summary>
    /// Gets the children of <paramref name="controlPanel"/> in tab order: by <see cref="IControlPanelDescriptor.Position"/>,
    /// then by <see cref="IControlPanelDescriptor.Title"/>.
    /// </summary>
    public static List<IControlPanelRegistryItem> GetChildren(this IEnumerable<IControlPanelRegistry> controlPanelRegistries,
        IControlPanelRegistryItem controlPanel)
    {
        if (controlPanel.AsParent is null)
            return [];

        return controlPanelRegistries
            .SelectMany(r => r)
            .Where(i => i.ChildOf == controlPanel.AsParent)
            .OrderBy(i => i.Descriptor.Position, new OptionalPositionComparer())
            .ThenBy(i => i.Descriptor.Title)
            .ToList();
    }
}
