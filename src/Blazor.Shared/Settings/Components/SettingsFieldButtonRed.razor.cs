using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.Button.Enums;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsFieldButtonRed
{
    [Parameter]
    public bool Busy { get; set; }

    [Parameter]
    public ButtonBusyIndication BusyIndication { get; set; } = ButtonBusyIndication.Sweep;

    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public string? IconCssClass { get; set; }

    [Parameter]
    public bool Enabled { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }
}
