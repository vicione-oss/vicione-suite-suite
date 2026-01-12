using AngleSharp.Dom;
using Bunit;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests;

internal static class IRenderedFragmentExtensions
{
    public static IElement FindDialogConfirmButton(this IRenderedFragment fragment)
        => fragment.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Confirm, StringComparison.Ordinal));

    public static IElement FindDialogCancelButton(this IRenderedFragment fragment)
        => fragment.FindAll("button").First(b => b.InnerHtml.Contains(CommonVocabulary.Cancel, StringComparison.Ordinal));
}
