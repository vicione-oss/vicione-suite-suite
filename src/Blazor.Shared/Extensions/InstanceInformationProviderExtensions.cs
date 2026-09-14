using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Sdk.Instance;

namespace Blazor.Shared.Extensions;

public static class InstanceInformationProviderExtensions
{
    public static MarkupString GetFormatedInstanceTitle(this IInstanceInformationProvider informationProvider)
    {
        var formattedTitle = HtmlEncoder.Default.Encode(informationProvider.Local.FormattedName);

        return new MarkupString(formattedTitle
            .Replace("{", "<span class=\"text-accent\">", StringComparison.Ordinal)
            .Replace("}", "</span>", StringComparison.Ordinal));
    }
}
