using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Tests;

/// <remarks>
/// Replaces the obsolete <c>Sdk.Testing.Client</c> helper of the same name. Being declared in an enclosing namespace
/// of the tests, it takes precedence over the imported SDK one.
/// </remarks>
internal static class IRenderedComponentExtensions
{
    extension<TComponent>(IRenderedComponent<TComponent> component) where TComponent : IComponent
    {
        /// <summary>
        /// Raises <c>oninput</c> on the selection checkbox of the first data row, skipping the header row.
        /// </summary>
        public void TriggerGridFirstRowSelectionChange(bool select)
        {
            const string ItemSelectCss = ".item-select-column";

            var firstDataRow = component.FindAll(ItemSelectCss).Skip(1).FirstOrDefault()
                ?? throw new ElementNotFoundException($"No Grid data row with class '{ItemSelectCss}' found.");

            var checkBox = firstDataRow.FindDescendant<IHtmlInputElement>()
                ?? throw new ElementNotFoundException("Grid select checkbox not found.");

            checkBox.TriggerEvent("oninput", new ChangeEventArgs { Value = select });
        }
    }
}
