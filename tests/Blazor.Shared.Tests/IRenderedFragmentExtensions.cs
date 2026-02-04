using AngleSharp.Dom;
using Bunit;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests;

internal static class IRenderedFragmentExtensions
{
    extension(IRenderedFragment fragment)
    {
        public IElement FindDialogConfirmButton()
            => fragment.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Confirm, StringComparison.Ordinal));

        public IElement FindDialogCancelButton()
            => fragment.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Cancel, StringComparison.Ordinal));
    }
}
