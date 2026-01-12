using Blazor.Shared.Profile.NotificationArea.Enums;
using Sdk.Client.Extensions;
using Sdk.Client.Modules;

namespace Blazor.Shared.Profile.NotificationArea.Extensions;

internal static class IconExtensions
{
    public static string GetPath(this Icon icon)
    {
        var modulePath = ModuleAssetHelper.GetModuleManifestName(typeof(Icon));
        return $"_content/{modulePath}/icons/{icon.ToString().ToHyphenSeparated()}.png";
    }
}
