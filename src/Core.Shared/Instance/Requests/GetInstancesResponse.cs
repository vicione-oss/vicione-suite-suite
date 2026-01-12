using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Instance.Requests;

public sealed record GetInstancesResponse(List<InstanceInformation> Instances, ErrorInfo? RequestError = null) : IResponse;
