using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Dialogs;

public sealed partial class ErrorDialog
{
    [Parameter] public Exception? Exception { get; set; }

    [Parameter] public bool IsDebugEnabled { get; set; }

    [Parameter] public bool Show { get; set; }

    [Parameter] public EventCallback OnConfirm { get; set; }
}
