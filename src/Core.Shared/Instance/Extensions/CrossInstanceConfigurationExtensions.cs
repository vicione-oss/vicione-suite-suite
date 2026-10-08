using System.Globalization;
using Core.Shared.Instance.Contracts;

namespace Core.Shared.Instance.Extensions;

public static class CrossInstanceConfigurationExtensions
{
    extension(CrossInstanceConfiguration)
    {
        /// <summary>
        /// Returns the supported culture with the given name, ignoring case like the request localization middleware,
        /// or <see langword="null"/> when the name is missing, invalid or not supported.
        /// </summary>
        public static CultureInfo? FindSupportedCulture(string? cultureName)
            => CrossInstanceConfiguration.SupportedCultures
                .FirstOrDefault(culture => culture.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase));
    }
}
