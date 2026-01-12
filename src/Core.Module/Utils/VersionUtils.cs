using System.Diagnostics.CodeAnalysis;

namespace Core.Module.Utils;

internal static class VersionUtils
{
    public static Version ParseVersion(string versionString)
        => TryParseVersion(versionString, out var parsed) ? parsed : new Version(0, 0, 0);

    public static bool TryParseVersion(string versionString, [NotNullWhen(true)] out Version? version)
    {
        // Even SemVer won't parse "-ci" version easily so this is a patch to get working
        var cleaned = versionString.EndsWith("-ci", StringComparison.Ordinal)
            ? versionString.Replace("-ci", string.Empty, StringComparison.Ordinal)
            : versionString;

        if (Version.TryParse(cleaned, out var parsed))
        {
            version = parsed;
            return true;
        }
        version = null;
        return false;
    }
}
