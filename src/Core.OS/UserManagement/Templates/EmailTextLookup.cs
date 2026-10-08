using System.Collections;
using System.Globalization;
using Core.OS.UserManagement.Templates.Localization;

namespace Core.OS.UserManagement.Templates;

/// <summary>
/// Reads <see cref="EmailTexts"/> in an explicit culture, as the generated properties read the current UI culture.
/// </summary>
internal static class EmailTextLookup
{
    public static string Get(string name, CultureInfo culture)
        => EmailTexts.ResourceManager.GetString(name, culture)
           ?? throw new KeyNotFoundException($"Email text '{name}' does not exist.");

    /// <summary>
    /// Returns every text keyed by its resource name, for use as <c>{{ Text.&lt;name&gt; }}</c> in a template.
    /// </summary>
    public static Dictionary<string, string> GetAll(CultureInfo culture)
        => EmailTexts.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)!
            .Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .ToDictionary(name => name, name => Get(name, culture));
}
