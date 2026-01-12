using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed class ControlPanelRequest(IEnumerable<IControlPanelRegistry> controlPanelRegistries)
    : IControlPanelRequest
{
    public event Func<ControlPanelRequestedEventArgs, Task>? ControlPanelRequested;

    public async Task<bool> Send(IControlPanelRegistryItem registryItem, Action? configureState = null)
    {
        if (ControlPanelRequested is not null)
        {
            var args = new ControlPanelRequestedEventArgs(registryItem,
                configureState);

            await ControlPanelRequested.Invoke(args);

            return !args.Cancel;
        }
        else
        {
            return false;
        }
    }

    public async Task<bool> Send<TComponent>()
        where TComponent : ComponentBase, IControlPanel
    {
        var controlPanelRegistryItem = controlPanelRegistries
            .SelectMany(r => r.Where(i => i.ComponentType == typeof(TComponent)))
            .FirstOrDefault();

        if (controlPanelRegistryItem is not null)
        {
            var result = await Send(controlPanelRegistryItem);

            return result;
        }
        else
        {
            return false;
        }
    }

    public async Task<bool> Send<TComponent, TState>(Action<TState>? configureState)
        where TComponent : ControlPanelBase<TState>, IControlPanel
        where TState : IControlPanelState
    {
        if (ControlPanelRequested is null)
            return false;

        var controlPanelRegistryItem = controlPanelRegistries
            .SelectMany(r => r.Where(i => i.ComponentType == typeof(TComponent)))
            .FirstOrDefault();

        if (controlPanelRegistryItem is not null && controlPanelRegistryItem.State is TState state)
        {
            var args = new ControlPanelRequestedEventArgs(controlPanelRegistryItem,
                configureState is not null ? () => configureState(state) : null);

            await ControlPanelRequested.Invoke(args);

            return !args.Cancel;
        }
        else
        {
            return false;
        }
    }
}
