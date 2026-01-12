using System.Globalization;

namespace Core.Shared.Instance.Contracts;

/// <summary>
/// Configuration that applies to all instances
/// </summary>
public sealed class CrossInstanceConfiguration
{
    public const string CultureNameDefault = "en-US";

    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Culture name in the format languagecode2-country/regioncode2.
    /// </summary>
    /// <remarks>
    /// languagecode2 is a lowercase two-letter code as defined
    /// in ISO 639-1, or, if no two-letter code is available, a three-letter code as
    /// defined in ISO 639-3. country/regioncode2 contains a value defined in ISO 3166
    /// and usually consists of two uppercase letters, or a BCP-47 language tag.
    /// </remarks>
    public string CultureName { get; set; } = CultureNameDefault;
    public string TimeZoneId { get; set; } = TimeZoneInfo.Utc.Id;

    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture, "Id:{0} CultureName:{1} TimeZone:{2}", Id, CultureName, TimeZoneId);
}
