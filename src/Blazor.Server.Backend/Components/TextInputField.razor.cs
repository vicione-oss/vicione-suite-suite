using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Components;

public sealed partial class TextInputField
{
    [Parameter, EditorRequired]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public Dictionary<string, object> Attributes { get; set; } = [];

    [Parameter, EditorRequired]
    public string? Placeholder { get; set; } = default!;

    [Parameter]
    public bool Valid { get; set; } = true;

    [Parameter]
    public bool Password { get; set; } = false;

    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }
}
