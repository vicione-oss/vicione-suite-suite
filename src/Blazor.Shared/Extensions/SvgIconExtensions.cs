using Blazor.Shared.Enums;
using Sdk.Client.Extensions;
using Sdk.Client.Modules;

namespace Blazor.Shared.Extensions;

public static class SvgIconExtensions
{
    public static Uri GetPath(this SvgIcon icon)
    {
        var iconPath = ModuleAssetHelper.GetModuleIconUrl<SharedClientModule>($"{icon.ToString().ToHyphenSeparated()}.svg");

        return iconPath;
    }
}
