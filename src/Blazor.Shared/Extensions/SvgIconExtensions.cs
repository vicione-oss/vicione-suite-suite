using Blazor.Shared.Enums;
using Sdk.Client.Extensions;
using Sdk.Client.Modules;

namespace Blazor.Shared.Extensions;

public static class SvgIconExtensions
{
    public static string GetPath(this SvgIcon icon)
    {
        var iconPath = ModuleAssetHelper.GetModuleIconPath<SharedClientModule>($"{icon.ToString().ToHyphenSeparated()}.svg");

        return iconPath;
    }
}
