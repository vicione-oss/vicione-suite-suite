using AngleSharp.Dom;
using Bunit;
using DevExpress.Blazor.Internal.Editors;

namespace Blazor.Shared.Tests;

public static class IElementExtensions
{
    extension(IElement editor)
    {
        public void DxSpinEditChange(string valueText)
            => editor.TriggerEvent("ondxbl-input-editor.textchange", new EditorTextChangeEventArgs { Text = valueText });

        public void DxTextEditInput(string text)
            => editor.TriggerEvent("ondxbl-input-editor.textinput", new EditorTextInputEventArgs { Text = text, Version = 0 });

        public void DxTextEditChange(string text)
            => editor.TriggerEvent("ondxbl-input-editor.textchange", new EditorTextChangeEventArgs { Text = text });
    }
}
