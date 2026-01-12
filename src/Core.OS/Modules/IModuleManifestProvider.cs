using Sdk.Modules;

namespace Core.OS.Modules;

public interface IModuleManifestProvider
{
    ModulePackageManifest GetManifest();

    Task UpdateManifestPackages(List<ModuleDependencyPackage> packages, CancellationToken cancellationToken = default);
}
