using System.Reflection;
using Core.Module.Utils;
using Mono.TextTemplating.CodeCompilation;

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
    /// <exception cref="FormatException">Thrown if one of the versions can't be parsed</exception>
    public static bool IsPatchUpdate(string oldVersion, string newVersion)
    {
        if (!SemVersion.TryParse(oldVersion, out var oldSemVersion))
            throw new FormatException($"Version can't be parsed: {oldVersion}");

        if (!SemVersion.TryParse(newVersion, out var newSemVersion))
            throw new FormatException($"Version can't be parsed: {oldVersion}");

        if (newSemVersion.Major != oldSemVersion.Major)
            return false;

        if (newSemVersion.Minor != oldSemVersion.Minor)
            return false;

        if (newSemVersion.Patch == oldSemVersion.Patch)
        {
            // e.g. old 0.2.4 can't be patched to version 0.2.4
            if (!oldSemVersion.IsPreRelease && !newSemVersion.IsPreRelease)
                return false;

            // e.g. old 0.2.4 can't be patched to ci version 0.2.4-ci123423
            if (!oldSemVersion.IsPreRelease && newSemVersion.IsPreRelease)
                return false;

            // e.g. old 0.2.4-ci123423 can be patched to released version 0.2.4
            if (oldSemVersion.IsPreRelease && !newSemVersion.IsPreRelease)
                return true;

            return string.CompareOrdinal(newSemVersion.PreRelease, oldSemVersion.PreRelease) > 0;
        }

        return newSemVersion.Patch > oldSemVersion.Patch;
    }
}
