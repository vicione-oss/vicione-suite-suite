using System.Globalization;

namespace Core.Shared;

public sealed class ConfigurationException : Exception
{
    private const string Format = "{0} is not configured";

    public ConfigurationException()
    {
    }

    public ConfigurationException(string configKey) : base(string.Format(CultureInfo.InvariantCulture, Format, configKey))
    {
    }

    public ConfigurationException(string configKey, Exception innerException) : base(string.Format(CultureInfo.InvariantCulture, Format, configKey), innerException)
    {
    }
}
