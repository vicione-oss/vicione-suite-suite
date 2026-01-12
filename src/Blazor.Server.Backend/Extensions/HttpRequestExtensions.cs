using Microsoft.AspNetCore.Http;

namespace Blazor.Server.Backend.Extensions;

internal static class HttpRequestExtensions
{
    public static string? BaseUrl(this HttpRequest? req)
    {
        if (req is null) return null;

        // https://learn.microsoft.com/en-us/dotnet/api/system.uribuilder.-ctor?view=net-7.0#system-uribuilder-ctor(system-string-system-string-system-int32)
        // If the portNumber is set to a value of -1, this indicates that the default port
        // value for the scheme will be used to connect to the host.
        var uriBuilder = new UriBuilder(req.Scheme, req.Host.Host, req.Host.Port ?? -1);
        if (uriBuilder.Uri.IsDefaultPort)
            uriBuilder.Port = -1;

        return uriBuilder.Uri.AbsoluteUri;
    }
}
