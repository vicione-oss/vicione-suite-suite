using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.Network.ControlPanels;

public class NetworkControlPanelBase<TState> : ControlPanelBase<TState>
    where TState : NetworkControlPanelStateBase;
