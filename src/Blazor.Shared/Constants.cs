using Sdk.Client.Components.Wallpaper.Enums;
using Sdk.Client.Modules;

namespace Blazor.Shared;

public static class Constants
{
    public const long BackupFileSizeLimitMB = 50 * 1024 * 1024;

    public const string IndexRoute = "/";

    public const string LogViewFeature = "Log viewer";
    public const string JournalViewRoute = "journal";

    public const string ProcessRoute = "processes";

    public const string UpdateLanguageCookieRoute = "api/UpdateLanguageCookie";

    public const string MqttViewerNavTileId = "4A8F8D39-6712-45F1-ACC9-C75BB77CE7BF";
    public const string MqttViewerRoute = "mqtt-viewer";

    public const string MoneoRcServiceNameConfigKey = "ViciOneSuiteMoneoConnect:RemoteConnect:ServiceName";

    /// <summary>
    /// this one might go to the appsettings so we could override it
    /// depending on the machine we are running on
    /// </summary>
    public const int CommandTimeoutMs = 10_000;

    public static readonly Uri WallpaperBaseUri = ModuleAssetHelper.GetModuleImageUrl<SharedClientModule>("wallpapers");
    public static readonly WallpaperImage WallpaperImage = WallpaperImage.BlackAbstractTriangles;

    public static string GetUserCultureCacheKey(string userId) => $"UserCulture_{userId}";
}
