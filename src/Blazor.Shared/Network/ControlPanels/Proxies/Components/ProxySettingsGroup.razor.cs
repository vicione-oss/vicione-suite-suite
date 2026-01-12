using Blazor.Shared.Network.ControlPanels.Proxies.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Components;

public sealed partial class ProxySettingsGroup : ComponentBase
{
    [Parameter, EditorRequired]
    public string Title { get; set; }
    [Parameter, EditorRequired]
    public ProxySettings Settings { get; set; }
    [Parameter]
    public EventCallback OnEdit { get; set; }

    private async Task BeginEdit()
    {
        if (OnEdit.HasDelegate)
            await OnEdit.InvokeAsync();
    }
}
