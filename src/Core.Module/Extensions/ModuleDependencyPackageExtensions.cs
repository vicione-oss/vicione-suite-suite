using Sdk.Modules;

namespace Core.Module.Extensions;

internal static class ModuleDependencyPackageExtensions
{
    public static string GetLocalMetadataFileName(this ModuleDependencyPackage package)
        => GetLocalMetadataFileName(package.Name);

    public static string GetLocalMetadataFileName(string packageName)
        => $"{packageName}.meta.json";
}
