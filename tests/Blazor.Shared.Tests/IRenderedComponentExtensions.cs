using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests;

internal static class IRenderedComponentExtensions
{
    extension<TComponent>(IRenderedComponent<TComponent> component) where TComponent : IComponent
    {
        public IElement FindDialogConfirmButton()
            => component.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Confirm, StringComparison.Ordinal));

        public IElement FindDialogCancelButton()
            => component.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Cancel, StringComparison.Ordinal));
    }
}
