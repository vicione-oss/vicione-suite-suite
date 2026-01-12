using System.Globalization;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Components.Localization.Extensions;

internal static class DateTimeOffsetExtensions
{
    /// <returns>
    /// <see href="https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#FullDateLongTime">Full date long time</see>
    /// when <paramref name="dateTimeOffset"/> is not null, otherwise <see cref="CommonVocabulary.Unknown"/>
    /// </returns>
    public static string LocalizeFullDateLongTime(this DateTimeOffset? dateTimeOffset)
        => dateTimeOffset.HasValue ? dateTimeOffset.Value.LocalizeFullDateLongTime() : CommonVocabulary.Unknown;

    /// <returns>
    /// <see href="https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#FullDateLongTime">Full date long time</see>
    /// </returns>
    public static string LocalizeFullDateLongTime(this DateTimeOffset dateTimeOffset)
        => dateTimeOffset.ToString("F", CultureInfo.CurrentCulture);

    /// <returns>
    /// <see href="https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#ShortDate">Short date</see>
    /// </returns>
    public static string LocalizeShortDate(this DateTimeOffset dateTimeOffset)
        => dateTimeOffset.ToString("d", CultureInfo.CurrentCulture);

    /// <returns>
    /// <see href="https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#ShortTime">Short time</see>
    /// </returns>
    public static string LocalizeShortTime(this DateTimeOffset dateTimeOffset)
        => dateTimeOffset.ToString("t", CultureInfo.CurrentCulture);

    public static DateTimeOffset? AdjustToTimeZone(this DateTimeOffset? dateTimeOffset, TimeZoneInfo timeZone)
    {
        if (dateTimeOffset is null)
            return null;

        return dateTimeOffset.Value.AdjustToTimeZone(timeZone);
    }

    public static DateTimeOffset AdjustToTimeZone(this DateTimeOffset dateTimeOffset, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeFromUtc(dateTimeOffset.UtcDateTime, timeZone);
}
