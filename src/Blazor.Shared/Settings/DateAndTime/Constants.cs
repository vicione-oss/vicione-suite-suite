namespace Blazor.Shared.Settings.DateAndTime;

internal sealed class Constants
{
    public static readonly TimeZoneInfo DefaultTimeZone = TimeZoneInfo.Utc;

    public static readonly string DefaultTimeZoneId = DefaultTimeZone.Id;
}
