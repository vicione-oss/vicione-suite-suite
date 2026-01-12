using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupContentActionButtonContainer
{
    internal const string CascadingEnabledParameterName = "CascadingEnabled";

    [Parameter] public bool ErrorMessageVisible { get; set; }

    [Parameter] public RenderFragment? ErrorMessage { get; set; }

    [Parameter] public required RenderFragment Buttons { get; set; }

    [Parameter] public bool ShowLoadingIndication { get; set; }
    [Parameter] public bool Enabled { get; set; } = true;
}
