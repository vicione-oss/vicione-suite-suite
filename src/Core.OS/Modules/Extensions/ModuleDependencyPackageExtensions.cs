using Sdk.Modules;

namespace Core.OS.Modules.Extensions;

internal static class ModuleDependencyPackageExtensions
{
    public static bool HasUnresolvedVersion(this ModuleDependencyPackage package)
        => package.Version is ModuleConstants.LatestVersionKey or ModuleConstants.UnresolvedVersionMarker;
}
