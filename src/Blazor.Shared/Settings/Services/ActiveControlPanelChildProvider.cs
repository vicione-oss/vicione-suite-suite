using Blazor.Shared.Settings.Extensions;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed class ActiveControlPanelChildProvider(IEnumerable<IControlPanelRegistry> controlPanelRegistries,
    SettingsModuleState settingsModuleState) : IActiveControlPanelChildProvider
{
    public IControlPanelRegistryItem? GetActiveChild(IControlPanelRegistryItem controlPanel)
    {
        // A control panel without an AsParent descriptor has no children to begin with.
        if (controlPanel.AsParent is null)
            return null;

        var children = controlPanelRegistries.GetChildren(controlPanel);

        if (settingsModuleState.ActiveChildMap.TryGetValue(controlPanel, out var activeChild) && children.Contains(activeChild))
            return activeChild;

        // Without a selection a control panel with content of its own is the active "tab" itself, the way its
        // content was tab #1 before this mechanism existed; one without content is never shown, so its first child is.
        return controlPanel.Descriptor.HasContent ? null : children.FirstOrDefault();
    }
}
