using Sdk.Modules;

namespace Core.OS.Modules;

public interface IModulePackageManifestStore
{
    Task<ModulePackageManifest> Load(CancellationToken cancellationToken);

    Task Store(ModulePackageManifest manifest, CancellationToken cancellationToken);
}
