using Core.OS.Instance.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.HealthCheck;
using Core.Shared.Instance.Requests;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Instance.Events;

namespace Core.OS.Instance.Consumers;

public sealed class ClusterInformationConsumer(InMemoryClusterInformationProvider clusterInformationProvider, ISuiteMediator mediator) :
    IConsumer<InstanceHealthInfo>,
    IConsumer<InstanceCreated>,
    IConsumer<ControlInstanceCompleted>
{
    private readonly InMemoryClusterInformationProvider _clusterInformationProvider = clusterInformationProvider;
    private readonly ISuiteMediator _mediator = mediator;

    public async Task Consume(ConsumeContext<ControlInstanceCompleted> context)
    {
        if (context.Message.Command == InstanceCommand.Delete)
            await _clusterInformationProvider.RemoveInstance(context.Message.InstanceId, context.Message.Error is null);
    }

    public async Task Consume(ConsumeContext<InstanceCreated> context)
    {
        var request = new GetInstances(context.Message.InstanceId);
        var response = await _mediator.Request<GetInstances, GetInstancesResponse>(request, context.CancellationToken);

        await _clusterInformationProvider.AddNewInstance(response.Instances.First());
    }

    public async Task Consume(ConsumeContext<InstanceHealthInfo> context)
        => await _clusterInformationProvider.ChangeHealthInfo(context.Message.SenderInstanceId, context.Message.Status, context.Message.WhenSentUtc);
}
