using Blazor.Wasm.Client.Infrastructure.Modules;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Modules;

namespace Blazor.Wasm.Client.Services;

internal sealed class ClientInstanceInformationProvider(IUiMediator mediator) :
    IInstanceInformationProvider
{
    private static InstanceInformation? _local;

    public IInstanceInformation Local => _local ?? throw new InvalidOperationException("InstanceInformationProvider not initialized");
    public IReadOnlyCollection<ModuleMetadata> InstalledModules { get; } = [];

    public async Task<List<IInstanceInformation>> GetInstancesInCluster(CancellationToken token)
    {
        var result = await mediator.Request<GetInstances, GetInstancesResponse>(new GetInstances(), token);
        return result.Instances.Cast<IInstanceInformation>().ToList();
    }

    public Task<IReadOnlyCollection<ModuleMetadata>> GetInstalledModules() => Task.FromResult<IReadOnlyCollection<ModuleMetadata>>([]);

    internal static async Task SetLocalInstanceInfo(IBackendModuleHttpClient client)
        => _local = await client.GetLocalInstanceInfo();
}
