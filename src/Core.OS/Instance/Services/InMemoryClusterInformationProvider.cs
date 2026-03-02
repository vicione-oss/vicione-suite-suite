using System.Collections.Concurrent;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sdk.Backend.Messaging;
using Sdk.Instance;

namespace Core.OS.Instance.Services;

public sealed class InMemoryClusterInformationProvider(ILogger<InMemoryClusterInformationProvider> logger) : IClusterInformationProvider
{
    private readonly ConcurrentDictionary<Guid, ClusterInstanceInformation> _instances = new();

    public event Func<IInstanceInformation, Task>? NewInstanceAdded;
    public event Func<Guid, HealthStatus, DateTimeOffset, Task>? HealthStatusChanged;
    public event Func<Guid, Task>? InstanceDeleted;
    public event Func<Guid, Task>? DeleteInstanceFailed;

    public Task<HealthStatus?> GetHealthStatus(Guid instanceId, CancellationToken token = default)
        => Task.FromResult(_instances[instanceId].HealthStatus);

    public Task<List<IInstanceInformation>> GetInstancesInCluster(CancellationToken token = default)
        => Task.FromResult(_instances.Select(i => i.Value.InstanceInformation).ToList());

    public Task<bool> IsClusterHealthy(CancellationToken token = default)
        => Task.FromResult(_instances.All(i => i.Value.HealthStatus == HealthStatus.Healthy));

    internal async Task Initialize(ISuiteMediator mediator, CancellationToken token)
    {
        var result = await mediator.Request<GetInstances, GetInstancesResponse>(new GetInstances(), token);

        logger.LogInformation("Initialize in memory information provider with {Count} instance(s)", result.Instances.Count);

        foreach (var instance in result.Instances)
            _instances.TryAdd(instance.Id, new(instance));
    }

    internal async Task AddNewInstance(IInstanceInformation instance)
    {
        if (_instances.All(i => i.Key != instance.Id))
        {
            if (_instances.TryAdd(instance.Id, new(instance)) && NewInstanceAdded is not null)
                await NewInstanceAdded.Invoke(instance);
        }
    }

    internal async Task ChangeHealthInfo(Guid senderInstanceId, HealthStatus status, DateTimeOffset whenSend)
    {
        var instance = _instances.FirstOrDefault(i => i.Key == senderInstanceId).Value;

        //Can happen during initialization of slave instances
        if (instance is null)
            return;

        instance.HealthStatus = status;

        if (HealthStatusChanged is not null)
            await HealthStatusChanged.Invoke(senderInstanceId, status, whenSend);
    }

    internal async Task RemoveInstance(Guid instanceId, bool success)
    {
        if (success && _instances.Remove(instanceId, out _) && InstanceDeleted is not null)
        {
            await InstanceDeleted.Invoke(instanceId);
            return;
        }

        if (DeleteInstanceFailed is not null)
            await DeleteInstanceFailed.Invoke(instanceId);
    }

    internal void UpdateInstanceInformation(IInstanceInformation instanceInformation)
    {
        if (!_instances.TryGetValue(instanceInformation.Id, out var instance))
            throw new InvalidOperationException($"Instance with id '{instanceInformation.Id}' does not exist.");

        var clusterInfo = new ClusterInstanceInformation(instanceInformation)
        {
            HealthStatus = instance.HealthStatus
        };

        if (!_instances.TryUpdate(instanceInformation.Id, clusterInfo, instance))
            throw new InvalidOperationException($"Failed to update instance with id '{instanceInformation.Id}'.");
    }
}
