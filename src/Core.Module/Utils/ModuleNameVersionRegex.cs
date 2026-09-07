using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Semver;

namespace Core.Module.Utils;

public static partial class ModuleNameVersionRegex
{
    /// <summary>
    /// https://regex101.com/r/muwDlB/3
    /// </summary>
    [GeneratedRegex(@"(?'version'\d+\.\d+\.\d+)-(?'civersion'(?:ci|rc)[0-9]+)?", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    /// <summary>
    /// https://regex101.com/r/gldEOw/3
    /// </summary>    
    [GeneratedRegex(@"^(?<version>\d+\.\d+\.\d+(\-(\d+|\w+))*)-(?<arch>(arm64|amd64|linux-x64|win-x64)+)_(?'sdkversion'\d+\.\d+\.\d+)\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex MetaMatcher();

    public static bool GetModuleVersion(string moduleName, [NotNullWhen(true)] out SemVersion? parsed)
    {
        parsed = null;

        // We expect names like:
        // 1.24.3-win-x64_0.19.0.json
        // 0.35.0-ci1733049-win-x64.zip
        // 0.35.1-rc1-win-x64.zip
        var match = Matcher().Match(moduleName);
        if (!match.Success)
            return false;

        // The ci part is optional so we have to check if it exists before we can parse the version.
        if (!match.Groups["civersion"].Success)
        {
            return SemVersion.TryParse(match.Groups["version"].Value, out parsed);
        }

        return SemVersion.TryParse($"{match.Groups["version"].Value}-{match.Groups["civersion"].Value}", out parsed);
    }

    public static bool TryParse(string moduleName,
        [NotNullWhen(true)] out SemVersion? moduleVersion,
        [NotNullWhen(true)] out string? architecture,
        [NotNullWhen(true)] out SemVersion? sdkVersion)
    {
        moduleVersion = sdkVersion = null;
        architecture = null;

        // e.g. 1.0.4[-ci24343243]_arm64_1.0.1.json
        var match = MetaMatcher().Match(moduleName);

        // Actually we only support arm64, amd64 and win-x64 and so other architectures don't get accepted
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
