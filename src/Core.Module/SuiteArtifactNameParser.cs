using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Semver;

namespace Core.Module;

internal static partial class SuiteArtifactNameParser
{
    //https://regex101.com/r/T04NnL/6
    [GeneratedRegex(@"^vicione-suite_(?<version>\d+\.\d+\.\d+(\~(\d+|\w+))*)_(?<arch>(arm64|amd64)+)\.deb(.minisig)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NameMatcher();

    //https://regex101.com/r/t7vwz8/3
    [GeneratedRegex(@"^vicione-suite_(?<version>\d+\.\d+\.\d+(\~(\d+|\w+))*)_(?<arch>(arm64|amd64)+)_(?'hmversion'\d+\.\d+\.\d+)\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex MetaMatcher();

    public static bool TryParse(string packageName,
        [NotNullWhen(true)] out SemVersion? suiteVersion,
        [NotNullWhen(true)] out string? architecture)
    {
        suiteVersion = null;
        architecture = null;

        // vicione-suite_1.0.4[~24343243]_arm64.deb[.minisig]
        var match = NameMatcher().Match(packageName);

        // Only arm64 and amd64 are supported; the regex rejects other architectures.
        if (!match.Success)
            return false;

        if (!SemVersion.TryParse(JFrogCiToNet(match.Groups["version"].Value), out suiteVersion))
            return false;

        // vicione-suite_1.0.4~1234_arm64.deb -> vicione-suite_1.0.4-ci1234_arm64.deb
        architecture = match.Groups["arch"].Value;

        return !string.IsNullOrEmpty(architecture);
    }

    public static bool TryParseMetadata(string packageName,
        [NotNullWhen(true)] out SemVersion? suiteVersion,
        [NotNullWhen(true)] out string? architecture,
        [NotNullWhen(true)] out SemVersion? hostManagementVersion)
    {
        suiteVersion = null;
        architecture = null;
        hostManagementVersion = null;

        // vicione-suite_1.0.4[~24343243]_arm64_1.1.1.json
        var match = MetaMatcher().Match(packageName);

        // Only arm64 and amd64 are supported; the regex rejects other architectures.
        if (!match.Success)
            return false;

        // vicione-suite_1.0.4~1234_arm64_1.1.1.json -> vicione-suite_1.0.4-ci1234_arm64_1.1.1.json
        if (!SemVersion.TryParse(JFrogCiToNet(match.Groups["version"].Value), out suiteVersion))
            return false;

        if (!SemVersion.TryParse(JFrogCiToNet(match.Groups["hmversion"].Value), out hostManagementVersion))
            return false;

        architecture = match.Groups["arch"].Value;

        return !string.IsNullOrEmpty(architecture);
    }

    private static string JFrogCiToNet(string str) => str.Replace("~", "-", StringComparison.Ordinal);
}
