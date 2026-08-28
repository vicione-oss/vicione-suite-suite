using Sdk.Messaging;

namespace Core.Shared.EnvironmentOverrides.Requests;

public sealed record GetEnvironmentOverrides : IInstanceDependentRequest<GetEnvironmentOverridesResponse>;
