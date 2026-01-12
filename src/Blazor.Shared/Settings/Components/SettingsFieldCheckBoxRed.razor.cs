using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsFieldCheckBoxRed
{
    [Parameter]
    public string Text { get; set; } = string.Empty;

    [Parameter]
    public bool Value { get; set; }

    [Parameter]
    public EventCallback<bool> ValueChanged { get; set; }
}
