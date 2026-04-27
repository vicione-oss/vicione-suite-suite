using System.Reflection;
using Core.Module.Comparer;
using Core.Module.Utils;
using Semver;

namespace Core.OS.Hosting;

internal static class SuiteVersionUtils
{
    /// <summary>
    /// Returns Major.Minor.Build from core assembly name
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static string GetSuiteVersion()
        => ModuleHelpers.GetNormalizedVersion(Assembly.GetExecutingAssembly());

    /// <summary>
    /// Gets the version from SDK assembly as [major].[minor].[build] string
    /// </summary>
    /// <returns>0.20.0.0 -> 0.20.0</returns>
    public static string GetSuiteSdkVersion()
        => ModuleHelpers.GetNormalizedVersion(ModuleHelpers.GetSdkAssemblyVersion());

    /// <summary>
    /// Evaluates if <paramref name="update"/> is equal or a minor or patch update for <paramref name="version"/> like here
    /// Version 10.2.4 is a minor update of 10.1.0 or 1.4.3-ci50000 is a minor update of 1.2.3-ci30000 or
    /// 1.1.2 is equal to 1.1.2
    /// </summary>
    /// <param name="version">Version string by SemVer specs 2.0.0</param>
    /// <param name="update">Version string by SemVer specs 2.0.0</param>
    /// <returns></returns>
    /// <exception cref="FormatException">Thrown if one of the versions can't be parsed</exception>
    public static bool IsVersionCompatible(string version, string update)
    {
        var origVersion = SemVersion.Parse(version);
        var updateVersion = SemVersion.Parse(update);

        // https://semver.org/#spec-item-8
        if (origVersion.Major != updateVersion.Major)
            return false;

        // is update equal or higher?
        return SemVersion.ComparePrecedence(origVersion, updateVersion) <= 0;
    }

    /// <summary>
    /// Evaluates if <paramref name="update"/> is a patch for <paramref name="version"/> like here
    /// Version 10.1.4 is a patch of 10.1.0 or 1.2.3-ci50000 is a patch of 1.2.3-ci30000
    /// </summary>
    /// <param name="version">Version string by SemVer specs 2.0.0</param>
    /// <param name="update">Version string by SemVer specs 2.0.0</param>
    /// <returns></returns>
    /// <exception cref="FormatException">Thrown if one of the versions can't be parsed</exception>
    public static bool IsPatchUpdate(string version, string update)
    {
        var origVersion = SemVersion.Parse(version);
        var updateVersion = SemVersion.Parse(update);

        return IsPatchUpdate(origVersion, updateVersion);
    }

    public static bool IsPatchUpdate(SemVersion version, SemVersion update)
    {
        // https://semver.org/#spec-item-8
        if (version.Major != update.Major)
            return false;

        // https://semver.org/#spec-item-7
        if (version.Minor != update.Minor)
            return false;

        return SemVersion.ComparePrecedence(version, update) == -1;
    }

    public static int Compare(string? x, string? y)
        => new StringVersionComparer().Compare(x, y);
}
