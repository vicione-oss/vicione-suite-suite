using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Settings.Components;

public partial class SettingsFieldBooleanIndicator
{
    /// <summary>
    /// Text rendered into <see href="https://html.spec.whatwg.org/#classes">class</see> attribute
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    [Parameter]
    public string TrueText { get; set; } = CommonVocabulary.Active;

    [Parameter]
    public string FalseText { get; set; } = Localization.SettingsFieldBooleanIndicator.Inactive;

    [Parameter]
    public bool State { get; set; }
}
