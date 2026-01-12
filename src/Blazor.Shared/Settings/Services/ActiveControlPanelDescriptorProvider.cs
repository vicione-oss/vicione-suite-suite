using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed class ActiveControlPanelDescriptorProvider(SettingsModuleState settingsModuleState)
    : IActiveControlPanelDescriptorProvider
{
    public IControlPanelDescriptor? GetActiveControlPanelDescriptor()
        => settingsModuleState.ActiveControlPanelRegistryItem?.Descriptor;
}
