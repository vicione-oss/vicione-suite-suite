using System.Reflection;
using Core.Module;
using Core.Module.Utils;

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
    /// Evaluates if <see cref="newVersion"/> is a patch for <see cref="oldVersion"/> like here
    /// Version 10.1.4 is a patch of 10.1.0 or 1.2.3-ci50000 is a patch of 1.2.3-ci30000
    /// </summary>
    /// <param name="oldVersion"></param>
    /// <param name="newVersion"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public static bool IsPatchUpdate(string oldVersion, string newVersion)
    {
        if (!MetadataAssetVersionRegex.GetVersions(oldVersion, out var oldVersionPart, out var oldCi) ||
            !Version.TryParse(oldVersionPart, out var oldParsed))
            throw new FormatException($"Version can't be parsed: {oldVersion}");

        if (!MetadataAssetVersionRegex.GetVersions(newVersion, out var newVersionPart, out var newCi) ||
            !Version.TryParse(newVersionPart, out var newParsed))
            throw new FormatException($"Version can't be parsed: {newVersion}");

        if (newParsed.Major != oldParsed.Major)
            return false;

        if (newParsed.Minor != oldParsed.Minor)
            return false;

        if (newParsed.Build == oldParsed.Build)
        {
            if (string.IsNullOrEmpty(oldCi) && string.IsNullOrEmpty(newCi))
                return false;

            // e.g. released version 0.2.4 is higher than 0.2.4-ci123423
            if (string.IsNullOrEmpty(oldCi))
                return false;

            if (string.IsNullOrEmpty(newCi))
                return true;

            return string.CompareOrdinal(newCi, oldCi) > 0;
        }

        return newParsed.Build > oldParsed.Build;
    }
}
