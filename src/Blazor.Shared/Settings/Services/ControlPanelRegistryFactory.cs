using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Modules;

namespace Blazor.Shared.Settings.Services;

internal sealed class ControlPanelRegistryFactory(IDefaultControlPanelGroupDescriptor _defaultControlPanelGroupDescriptor) : IControlPanelRegistryFactory
{
    public IControlPanelRegistry<TClientModule> CreateControlPanelRegistry<TClientModule>() where TClientModule : class, IClientModule
        => new ControlPanelRegistry<TClientModule>(_defaultControlPanelGroupDescriptor);
}
