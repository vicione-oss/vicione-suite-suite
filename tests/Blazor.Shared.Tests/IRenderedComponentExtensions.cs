using AngleSharp.Dom;
using Bunit;
using DevExpress.Blazor;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests;

internal static class IRenderedComponentExtensions
{
    extension(IRenderedComponent<IComponent> component)
    {
        public void AssertDxTextBoxValue(string id, string? expectedValue)
        {
            var textBoxes = component.FindComponents<DxTextBox>();
            var textBox = textBoxes.First(k => k.Instance.Id == id);
            textBox.Instance.Text.Should().Be(expectedValue);
        }

        public void AssertDxComboSelectedValue<TData, TValue>(string id, TValue expectedValue)
        {
            var comboBoxes = component.FindComponents<DxComboBox<TData, TValue>>();
            var comboBox = comboBoxes.First(k => k.Instance.Id == id);
            comboBox.Instance.Value.Should().BeEquivalentTo(expectedValue);
        }

        public void AssertDxTagBoxItems(string id, IEnumerable<string> tags)
        {
            var tagBoxes = component.FindComponents<DxTagBox<string, string>>();
            var tagBox = tagBoxes.First(k => k.Instance.Id == id);
            tagBox.Instance.Tags.Should().BeEquivalentTo(tags);
        }
    }
    

    extension<TComponent>(IRenderedComponent<TComponent> component) where TComponent : IComponent
        {
        public IElement GetButtonByInnerHtml(string text) => component.FindAll("button").First(b => b.InnerHtml.Contains(text, StringComparison.Ordinal));

        public IElement GetFooterButton(string text)
            => component
                .FindAll(".btn-footer")
                .First(b => b.InnerHtml.Contains(text, StringComparison.Ordinal));
        
        public IElement FindDialogConfirmButton()
            => component.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Confirm, StringComparison.Ordinal));

        public IElement FindDialogCancelButton()
            => component.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Cancel, StringComparison.Ordinal));
    }

    public static void DxSpinEditChange<TValue>(this IRenderedComponent<DxSpinEdit<TValue>> spinEdit, TValue text)
    {
        var element = spinEdit.Nodes.First(k => k is IElement) as IElement ?? throw new InvalidOperationException();
        element.DxSpinEditChange(text?.ToString() ?? string.Empty);
    }
}
