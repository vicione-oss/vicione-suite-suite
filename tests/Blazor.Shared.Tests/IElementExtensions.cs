using AngleSharp.Dom;
using Bunit;
using DevExpress.Blazor.Internal.Editors;

namespace Blazor.Shared.Tests;

public static class IElementExtensions
{
    public static void DxSpinEditChange(this IElement editor, string valueText)
        => editor.TriggerEvent("ondxbl-input-editor.textchange", new EditorTextChangeEventArgs { Text = valueText });

    public static void DxTextEditInput(this IElement editor, string text)
        => editor.TriggerEvent("ondxbl-input-editor.textinput", new EditorTextInputEventArgs { Text = text, Version = 0 });

    public static void DxTextEditChange(this IElement editor, string text)
        => editor.TriggerEvent("ondxbl-input-editor.textchange", new EditorTextChangeEventArgs { Text = text });
}
