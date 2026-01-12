using Blazor.Server.Backend.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.Server.Backend.Components;

public partial class AccountButton
{
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public AccountButtonType ButtonType { get; set; } = AccountButtonType.Default;

    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; } = default!;

    [Parameter]
    public bool Submit { get; set; }

    [Parameter]
    public bool Disabled { get; set; }
}
