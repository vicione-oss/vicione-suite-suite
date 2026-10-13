using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Semver;

namespace Core.Module.Utils;

public static partial class ModuleNameVersionRegex
{
    /// <remarks>
    /// Module runtime identifiers plus the suite package architectures; <c>linux-arm64</c> precedes <c>arm64</c>.
    /// </remarks>
    private const string Architectures = "linux-x64|linux-arm64|win-x64|arm64|amd64";

    /// <remarks>
    /// The lazy prerelease group stops at the first architecture, so <c>linux</c> is never taken as a prerelease label.
    /// </remarks>
    private const string VersionPattern = @"^(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z]+)*?)-(?<arch>" + Architectures + ")";

    [GeneratedRegex(VersionPattern + @"(?:_|\.zip$)", RegexOptions.CultureInvariant)]
    private static partial Regex Matcher();

    [GeneratedRegex(VersionPattern + @"_(?<sdkversion>\d+\.\d+\.\d+)\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex MetaMatcher();

    public static bool GetModuleVersion(string moduleName, [NotNullWhen(true)] out SemVersion? parsed)
    {
        parsed = null;

        // Expected names:
        // 1.24.3-win-x64_0.19.0.json
        // 0.35.0-ci1733049-win-x64.zip
        // 3.0.0-beta2-linux-arm64_3.0.0.json
        var match = Matcher().Match(moduleName);
        if (!match.Success)
            return false;

        return SemVersion.TryParse(match.Groups["version"].Value, out parsed);
    }

    public static bool TryParse(string moduleName,
        [NotNullWhen(true)] out SemVersion? moduleVersion,
        [NotNullWhen(true)] out string? architecture,
        [NotNullWhen(true)] out SemVersion? sdkVersion)
    {
        moduleVersion = sdkVersion = null;
        architecture = null;

        // e.g. 1.0.4[-ci24343243]-arm64_1.0.1.json
        var match = MetaMatcher().Match(moduleName);

        // Only the architectures in Architectures are supported; the regex rejects any other.
        if (!match.Success)
            return false;

        if (!SemVersion.TryParse(match.Groups["version"].Value, out moduleVersion))
            return false;

        if (!SemVersion.TryParse(match.Groups["sdkversion"].Value, out sdkVersion))
            return false;

        architecture = match.Groups["arch"].Value;

        return !string.IsNullOrEmpty(architecture);
    }
}
