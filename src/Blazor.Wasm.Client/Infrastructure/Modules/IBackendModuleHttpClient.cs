using System.Globalization;
using Core.Shared.Instance.Contracts;
using Sdk.Modules;

namespace Blazor.Wasm.Client.Infrastructure.Modules;

public interface IBackendModuleHttpClient
{
    Task<List<ModuleMetadata>> GetModuleMetadata(CancellationToken token = default);

    Task<ModuleMetadata?> GetModuleMetadata(string moduleId, CancellationToken token = default);

    Task<InstanceInformation?> GetLocalInstanceInfo(CancellationToken token = default);

    Task<byte[]> LoadClientModulesArchive(IEnumerable<string?> assemblyNames, CancellationToken token = default);

    Task<byte[]> LoadClientModuleResourcesArchive(CultureInfo cultureInfo, CancellationToken token = default);
}
