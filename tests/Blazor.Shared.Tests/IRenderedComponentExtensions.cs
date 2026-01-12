using AngleSharp.Dom;
using Bunit;
using DevExpress.Blazor;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Tests;

internal static class IRenderedComponentExtensions
{
    public static void AssertDxTextBoxValue<TComponent>(this IRenderedComponent<TComponent> component, string id, string? expectedValue)
        where TComponent : IComponent
    {
        var textBoxes = component.FindComponents<DxTextBox>();
        var textBox = textBoxes.First(k => k.Instance.Id == id);
        textBox.Instance.Text.Should().Be(expectedValue);
    }

    public static void AssertDxComboSelectedValue<TComponent, TData, TValue>(this IRenderedComponent<TComponent> component, string id, TValue expectedValue)
        where TComponent : IComponent
    {
        var comboBoxes = component.FindComponents<DxComboBox<TData, TValue>>();
        var comboBox = comboBoxes.First(k => k.Instance.Id == id);
        comboBox.Instance.Value.Should().BeEquivalentTo(expectedValue);
    }

    public static void AssertDxTagBoxItems<TComponent>(this IRenderedComponent<TComponent> component, string id, IEnumerable<string> tags)
        where TComponent : IComponent
    {
        var tagBoxes = component.FindComponents<DxTagBox<string, string>>();
        var tagBox = tagBoxes.First(k => k.Instance.Id == id);
        tagBox.Instance.Tags.Should().BeEquivalentTo(tags);
    }

    public static void DxSpinEditChange<TValue>(this IRenderedComponent<DxSpinEdit<TValue>> spinEdit, TValue text)
    {
        var element = spinEdit.Nodes.First(k => k is IElement) as IElement ?? throw new InvalidOperationException();
        element.DxSpinEditChange(text?.ToString() ?? string.Empty);
    }

    public static IElement GetButtonByInnerHtml<TComponent>(this IRenderedComponent<TComponent> component, string text)
        where TComponent : IComponent
        => component.FindAll("button").First(b => b.InnerHtml.Contains(text, StringComparison.Ordinal));

    public static IElement GetFooterButton<TComponent>(this IRenderedComponent<TComponent> component, string text)
        where TComponent : IComponent
        => component
            .FindAll(".btn-footer")
            .First(b => b.InnerHtml.Contains(text, StringComparison.Ordinal));
}
