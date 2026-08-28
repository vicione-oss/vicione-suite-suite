using Sdk.Messaging;

namespace Core.Shared.EnvironmentOverrides.Requests;

public sealed record GetEnvironmentOverridesResponse(Dictionary<string, string> Overrides, ErrorInfo? RequestError = null) : IResponse;
