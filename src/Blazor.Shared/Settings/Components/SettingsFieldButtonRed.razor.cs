using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsFieldButtonRed
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public string? IconCssClass { get; set; }

    [Parameter]
    public bool Enabled { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }
}
