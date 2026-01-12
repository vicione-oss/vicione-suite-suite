using Core.Module;
using Sdk.Modules;

namespace Core.OS.Modules;

public interface IModuleOptionsStore
{
    Task Store(string moduleId, List<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default);

    Task<IConfiguration?> LoadJsonConfiguration(string moduleId, CancellationToken cancellationToken = default);

    Task<Dictionary<string, string?>> LoadJsonDictionary(string moduleId, CancellationToken cancellationToken = default);

    Dictionary<string, string?> LoadDictionarySync(string moduleId);

    Task ValidateModuleOptions(SuiteDependencyContext context, CancellationToken cancellationToken = default);
}
