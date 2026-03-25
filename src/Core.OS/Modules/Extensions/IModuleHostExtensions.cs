using Core.UiHosting;

namespace Core.OS.Modules.Extensions;

internal static class IModuleHostExtensions
{
    public static bool SetUiHostCulture(this IModuleHost moduleHost, string? cultureName)
    {
        var uiHost = (IUiHostModule?)moduleHost.GetModules().FirstOrDefault(m => m is IUiHostModule);
        if (uiHost is null)
            return false;

        uiHost.SetDefaultRequestCulture(cultureName);
        return true;
    }

    public static string? GetUiHostCulture(this IModuleHost moduleHost)
    {
        var uiHost = (IUiHostModule?)moduleHost.GetModules().FirstOrDefault(m => m is IUiHostModule);
        if (uiHost is null)
            return null;

        return uiHost.GetDefaultRequestCulture();
    }
}
