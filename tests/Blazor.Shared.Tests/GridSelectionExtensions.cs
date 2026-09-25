using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Tests;

internal static class GridSelectionExtensions
{
    extension<TComponent>(IRenderedComponent<TComponent> component) where TComponent : IComponent
    {
        /// <summary>
        /// Ticks the selection checkbox of the first data row; the first <c>.item-select-column</c> belongs to the header row.
        /// </summary>
        public void SelectFirstGridRow()
        {
            var checkbox = component.FindAll(".item-select-column").Skip(1).First().FindDescendant<IHtmlInputElement>()
                ?? throw new ElementNotFoundException("Grid select checkbox not found.");

            checkbox.TriggerEvent("oninput", new ChangeEventArgs { Value = true });
        }
    }
}
