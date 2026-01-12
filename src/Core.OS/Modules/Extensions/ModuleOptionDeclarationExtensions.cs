using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class ModuleOptionDeclarationExtensions
{
    /// <summary>
    /// Transform e.g. moduleId is ViciOne.Suite.ClusterManagement -> ViciOneSuiteClusterManagement
    /// because bash can't set environment variables containing a dot
    /// https://unix.stackexchange.com/questions/93532/exporting-a-variable-with-dot-in-it
    /// </summary>
    /// <param name="options"></param>
    /// <param name="moduleId"></param>
    /// <returns></returns>
    public static IEnumerable<KeyValuePair<string, string?>> AsConfiguration(this List<ModuleOptionDeclaration> options, string moduleId)
        => options.Select(k => new KeyValuePair<string, string?>(k.GetOptionKey(moduleId), k.Value));

    public static string GetOptionKey(this ModuleOptionDeclaration option, string moduleId)
        => $"{moduleId.Replace(".", "", StringComparison.Ordinal)}:{option.Key}";
}
