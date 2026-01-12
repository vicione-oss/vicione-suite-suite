using Sdk.Messaging;

namespace Core.Shared.Instance.Requests;

public sealed record GetInstances(Guid? InstanceId = null) : IRequest<GetInstancesResponse>;
