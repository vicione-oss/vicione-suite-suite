using Blazor.Server.Backend.Enums;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Server.Backend.Components;

public partial class AccountButton
{
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    [Parameter]
    public MonochromeIconName? IconName { get; set; }

    [Parameter]
    public AccountButtonType ButtonType { get; set; } = AccountButtonType.Default;

    [Parameter]
    public string Form { get; set; } = string.Empty;

    [Parameter]
    public string? Name { get; set; } = null!;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Title { get; set; }

    private string CssClass
        => $"account-button {ButtonType.ToString().ToHyphenSeparated()}"
           + (IconName is null ? string.Empty : " with-icon");
}
