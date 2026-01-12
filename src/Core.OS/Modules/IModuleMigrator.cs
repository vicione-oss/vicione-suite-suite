using Sdk.Modules;

namespace Core.OS.Modules;

public interface IModuleMigrator
{
    Task PrepareUpdateMigration(ModulePackageManifest manifest, CancellationToken cancellationToken = default);
}
