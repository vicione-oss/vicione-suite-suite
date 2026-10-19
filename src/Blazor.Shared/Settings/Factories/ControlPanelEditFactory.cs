using Blazor.Shared.Settings.Models;
using Sdk.Client.ControlPanels;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Factories;

internal sealed class ControlPanelEditFactory(IServiceProvider serviceProvider)
{
    public IControlPanelEdit CreateControlPanelEdit(IControlPanelRegistryItem controlPanelRegistryItem)
    {
        var controlPanelState = controlPanelRegistryItem.State;

        var controlPanelEditType = typeof(ControlPanelEdit<>).MakeGenericType(controlPanelState.GetType());
        var handlerServiceKey = typeof(ControlPanelServiceKey<>).MakeGenericType(controlPanelRegistryItem.ComponentType);

        if (Activator.CreateInstance(controlPanelEditType, [controlPanelState, serviceProvider, handlerServiceKey]) is not IControlPanelEdit result)
            throw new InvalidCastException();

        return result;
    }
}
