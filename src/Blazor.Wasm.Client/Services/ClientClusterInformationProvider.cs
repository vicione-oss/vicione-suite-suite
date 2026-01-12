using System.Collections.Concurrent;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.HealthCheck;
using Core.Shared.Instance.Requests;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Instance.Events;

namespace Blazor.Wasm.Client.Services;

public sealed class ClientClusterInformationProvider :
    IClusterInformationProvider,
    IEventConsumer<InstanceHealthInfo>,
    IEventConsumer<InstanceCreated>,
    IEventConsumer<InstanceAdministrated>,
    IDisposable
{
    private readonly IUiMediator _mediator;
    private readonly List<IDisposable> _subscriptions = [];
    private readonly ConcurrentDictionary<Guid, ClusterInstanceInformation> _instances = new();
    private bool _isInitialized;

    public event Func<IInstanceInformation, Task>? NewInstanceAdded;
    public event Func<Guid, HealthStatus, DateTime, Task>? HealthStatusChanged;
    public event Func<Guid, Task>? InstanceDeleted;
    public event Func<Guid, Task>? DeleteInstanceFailed;

    public ClientClusterInformationProvider(IUiMediator mediator)
    {
        _mediator = mediator;
        _subscriptions.Add(_mediator.Register<InstanceHealthInfo>(this));
        _subscriptions.Add(_mediator.Register<InstanceCreated>(this));
        _subscriptions.Add(_mediator.Register<InstanceAdministrated>(this));
    }

    public async Task<HealthStatus?> GetHealthStatus(Guid instanceId)
    {
        await EnsureInitialized(CancellationToken.None);

        return _instances[instanceId].HealthStatus;
    }

    public async Task<List<IInstanceInformation>> GetInstancesInCluster(CancellationToken token)
    {
        await EnsureInitialized(token);

        return _instances.Select(i => i.Value.InstanceInformation).ToList();
    }

    public async Task<bool> IsClusterHealthy()
    {
        await EnsureInitialized(CancellationToken.None);

        return _instances.All(i => i.Value.HealthStatus == HealthStatus.Healthy);
    }

    public async Task Consume(ClientContext<InstanceCreated> context, CancellationToken cancellationToken)
    {
        await EnsureInitialized(cancellationToken);

        if (_instances.All(i => i.Key != context.Message.InstanceId))
        {
            var instance = (await _mediator.Request<GetInstances, GetInstancesResponse>(new GetInstances(context.Message.InstanceId), CancellationToken.None)).Instances.First();

            if (_instances.TryAdd(context.Message.InstanceId, new(instance)) && NewInstanceAdded is not null)
                await NewInstanceAdded.Invoke(instance);
        }
    }

    public async Task Consume(ClientContext<InstanceHealthInfo> context, CancellationToken cancellationToken)
    {
        await EnsureInitialized(cancellationToken);

        var instance = _instances.FirstOrDefault(i => i.Key == context.Message.SenderInstanceId).Value;

        //Can happen during initialization of slave instances
        if (instance is null)
            return;

        instance.HealthStatus = context.Message.Status;

        if (HealthStatusChanged is not null)
            await HealthStatusChanged.Invoke(context.Message.SenderInstanceId, context.Message.Status, context.Message.WhenSentUtc);
    }

    public async Task Consume(ClientContext<InstanceAdministrated> context, CancellationToken cancellationToken)
    {
        await EnsureInitialized(cancellationToken);

        if (context.Message.Success && _instances.Remove(context.Message.InstanceId, out _) && InstanceDeleted is not null)
            await InstanceDeleted.Invoke(context.Message.InstanceId);

        if (DeleteInstanceFailed is not null)
            await DeleteInstanceFailed.Invoke(context.Message.InstanceId);
    }

    private async Task EnsureInitialized(CancellationToken token)
    {
        if (!_isInitialized)
        {
            await Initialize(token);

            _isInitialized = true;
        }
    }

    private async Task Initialize(CancellationToken token)
    {
        var result = await _mediator.Request<GetInstances, GetInstancesResponse>(new GetInstances(), token);

        foreach (var instance in result.Instances)
            _instances.TryAdd(instance.Id, new(instance));
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();
        _instances.Clear();
    }
}
