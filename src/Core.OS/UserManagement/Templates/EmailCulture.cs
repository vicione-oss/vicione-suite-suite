using System.Globalization;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Extensions;

namespace Core.OS.UserManagement.Templates;

/// <summary>Resolves the language of a user management email.</summary>
internal static class EmailCulture
{
    /// <summary>
    /// Returns the user's language, otherwise the instance default,
    /// otherwise the first of <see cref="CrossInstanceConfiguration.SupportedCultures"/>.
    /// Unsupported or invalid culture names are skipped.
    /// </summary>
    public static CultureInfo Resolve(string? userLanguage, string? instanceCultureName)
        => CrossInstanceConfiguration.FindSupportedCulture(userLanguage)
           ?? CrossInstanceConfiguration.FindSupportedCulture(instanceCultureName)
           ?? CrossInstanceConfiguration.SupportedCultures[0];
}
