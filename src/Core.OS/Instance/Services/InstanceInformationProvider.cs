using Core.OS.Modules;
using Core.Shared.Instance.Requests;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Instance.Events;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Instance.Services;

[ReadOnlyConsumer]
internal sealed class InstanceInformationProvider(ISuiteMediator mediator, ILocalInstanceInformationProvider localProvider, IModuleMetadataCache metadataCache) :
    IInstanceInformationProvider, IConsumer<InstanceCreated>
{
    public IInstanceInformation Local => localProvider.Local;

    public async Task<IReadOnlyCollection<ModuleMetadata>> GetInstalledModules()
        => (await metadataCache.GetInstalledModuleMetadata())
        .Select(k => k.Metadata)
        .ToList().AsReadOnly();

    public async Task<List<IInstanceInformation>> GetInstancesInCluster(CancellationToken token)
    {
        var result = await mediator.Request<GetInstances, GetInstancesResponse>(new GetInstances(), token);
        return [.. result.Instances];
    }

    public async Task Consume(ConsumeContext<InstanceCreated> context)
    {
        if (context.Message.InstanceId != Local.Id)
            return;

        var result = (await mediator
                .Request<GetInstances, GetInstancesResponse>(new GetInstances(context.Message.InstanceId), context.CancellationToken))
            .Instances.SingleOrDefault();

        if (result is not null)
        {
            localProvider.UpdateLocal(result);
        }
    }
}
