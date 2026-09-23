using System.Globalization;

namespace Core.Shared.Instance.Contracts;

public sealed class CrossInstanceConfiguration
{
    public const string CultureNameDefault = "en-US";

    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Culture name in the format languagecode2-country/regioncode2, or a BCP-47 language tag.
    /// </summary>
    public string CultureName { get; set; } = CultureNameDefault;
    public string TimeZoneId { get; set; } = TimeZoneInfo.Utc.Id;

    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture, "Id:{0} CultureName:{1} TimeZone:{2}", Id, CultureName, TimeZoneId);
}
