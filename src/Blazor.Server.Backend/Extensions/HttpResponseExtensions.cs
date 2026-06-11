using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace Blazor.Server.Backend.Extensions;

public static class HttpResponseExtensions
{
    public static IResult ToLocalRedirect([StringSyntax("Uri", UriKind.Relative)] this string localPath)
        => Results.LocalRedirect(localPath);
}
