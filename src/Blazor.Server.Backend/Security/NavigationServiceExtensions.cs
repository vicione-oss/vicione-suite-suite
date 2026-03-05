using System.Text;
using Blazor.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace Blazor.Server.Backend.Security;

public static class NavigationServiceExtensions
{
    public static string CreateCallbackLink(this INavigationService navigationService,
        string route,
        string userId,
        string code)
    {
        var uriWithoutQuery = navigationService.NavManager.ToAbsoluteUri(route).GetLeftPart(UriPartial.Path);
        var newUri = navigationService.NavManager.GetUriWithQueryParameters(
            uriWithoutQuery,
            new Dictionary<string, object?>()
            {
                { "userId", userId },
                { "code", WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code)) }
            });

        return newUri ?? throw new InvalidOperationException("Failed to generate callback link");
    }
}
