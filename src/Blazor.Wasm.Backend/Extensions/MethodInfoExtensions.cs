using System.Reflection;

namespace Blazor.Wasm.Backend.Extensions;

internal static class MethodInfoExtensions
{
    public static async Task Invoke(this MethodInfo methodInfo, object obj, params object[] parameters)
    {
        dynamic awaitable = methodInfo.Invoke(obj, parameters) ?? Task.CompletedTask;
        await awaitable;
    }
}
