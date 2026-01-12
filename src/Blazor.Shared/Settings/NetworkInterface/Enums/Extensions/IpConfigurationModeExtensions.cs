namespace Blazor.Shared.Settings.NetworkInterface.Enums.Extensions;

internal static class IpConfigurationModeExtensions
{
    public static string ToLocalizedString(this IpConfigurationMode ipConfigurationMode)
    {
        var ipConfigurationModeStr = ipConfigurationMode.ToString();

        return Localization.IpConfigurationMode.ResourceManager.GetString(ipConfigurationModeStr,
            Localization.IpConfigurationMode.Culture) ?? ipConfigurationModeStr;
    }
}
