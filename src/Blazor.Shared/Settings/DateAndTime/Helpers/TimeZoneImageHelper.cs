using Sdk.Client.Modules;

namespace Blazor.Shared.Settings.DateAndTime.Helpers;

internal static class TimeZoneImageHelper
{
    public static string GetTimeZoneImageSrc(string filename)
        => ModuleAssetHelper.GetModuleImagePath<SharedClientModule>($"time-zones/{filename}");
}
