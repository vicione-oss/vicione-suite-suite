using System.Text;
using Core.Module.Options;

namespace Core.Module.Extensions;

internal static class HttpClientExtension
{
    public static void AddDefaultRequestHeaders(this HttpClient client, ModuleApiOptions apiOptions)
    {
        if (!string.IsNullOrEmpty(apiOptions.UserName) && !string.IsNullOrEmpty(apiOptions.Password))
        {
            var byteArray = Encoding.ASCII.GetBytes($"{apiOptions.UserName}:{apiOptions.Password}");
            client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(byteArray));
        }
    }
}
