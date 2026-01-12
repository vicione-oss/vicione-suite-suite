using Blazor.Shared.Settings.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Factories;

internal sealed class ControlPanelEditFactory(IServiceProvider serviceProvider)
{
    public IControlPanelEdit CreateControlPanelEdit(IControlPanelState controlPanelState)
    {
        var controlPanelEditType = typeof(ControlPanelEdit<>).MakeGenericType(controlPanelState.GetType());

        if (Activator.CreateInstance(controlPanelEditType, [controlPanelState, serviceProvider]) is not IControlPanelEdit result)
            throw new InvalidCastException();

        return result;
    }
}
