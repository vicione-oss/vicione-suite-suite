using Sdk.Client.Modules;

namespace Blazor.Shared.Settings.DateAndTime.Helpers;

internal static class TimeZoneImageHelper
{
    public static Uri GetTimeZoneImageUri(string filename)
        => ModuleAssetHelper.GetModuleImageUrl<SharedClientModule>($"time-zones/{filename}");
}
