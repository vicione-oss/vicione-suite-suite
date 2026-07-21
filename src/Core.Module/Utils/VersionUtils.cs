using System.Diagnostics.CodeAnalysis;

namespace Core.Module.Utils;

internal static class VersionUtils
{
    public static Version ParseVersion(string versionString)
        => TryParseVersion(versionString, out var parsed) ? parsed : new Version(0, 0, 0);

    public static bool TryParseVersion(string versionString, [NotNullWhen(true)] out Version? version)
    {
        // System.Version can't parse SemVer pre-release/build metadata (e.g. "-ci1733049", "-rc1", "+build").
        // Strip everything from the first '-' or '+' so we parse the core Major.Minor.Build.
        var separatorIndex = versionString.IndexOfAny(['-', '+']);
        var cleaned = separatorIndex >= 0
            ? versionString[..separatorIndex]
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
