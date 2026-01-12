using Core.Module.Utils;
using Microsoft.Extensions.DependencyModel;

namespace Core.Module.Extensions;

public static class RuntimeLibraryExtensions
{
    public static bool IsMatch(this RuntimeLibrary rt, string name, string version) => rt.Name == name && rt.Version == version;

    public static bool IsMatch(this Dependency dp, string name, string version) => dp.Name == name && dp.Version == version;

    public static bool HasNameVersionKey(this RuntimeLibrary rt, string nameVersionKey)
        => ModuleHelpers.GetNameVersionKey(rt.Name, rt.Version) == nameVersionKey;

    public static bool HasNameVersionKey(this Dependency dp, string nameVersionKey) => ModuleHelpers.GetNameVersionKey(dp.Name, dp.Version) == nameVersionKey;

    public static Version ParseVersion(this RuntimeLibrary rt)
        => VersionUtils.ParseVersion(rt.Version);
}
