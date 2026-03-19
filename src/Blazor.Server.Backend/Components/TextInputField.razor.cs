using System.Linq.Expressions;
using Blazor.Server.Backend.Enums;
using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Components;

public sealed partial class TextInputField
{
    private string _inputId = string.Empty;

    [Parameter, EditorRequired]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public string AutoComplete { get; set; } = default!;

    [Parameter]
    public bool Required { get; set; }

    [Parameter, EditorRequired]
    public string? Placeholder { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter]
    public bool Valid { get; set; } = true;

    [Parameter]
    public bool Password { get; set; } = false;

    [Parameter]
    public bool Enabled { get; set; } = true;

    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    [Parameter]
    public TextInputFieldTypes Type { get; set; } = TextInputFieldTypes.Default;

    protected override void OnInitialized()
        => _inputId = Guid.NewGuid().ToString();

    private void TextBoxValueChanged()
        => ValueChanged.InvokeAsync(Value);
}
