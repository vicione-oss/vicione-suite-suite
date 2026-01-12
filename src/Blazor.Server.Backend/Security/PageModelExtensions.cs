using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Blazor.Server.Backend.Security;

public static class PageModelExtensions
{
    public static string CreateCallbackLink(this PageModel pageModel,
        string pageName,
        string userId,
        string code)
    {
        return pageModel.Url.Page(pageName: pageName,
            pageHandler: null,
            values: new
            {
                area = "Identity",
                userId,
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code))
            },
            protocol: pageModel.Request.Scheme,
            host: pageModel.Request.Host.Value
        ) ?? throw new InvalidOperationException("Failed to generate callback link");
    }
}
