using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Instance.Requests;

public sealed record GetCrossInstanceConfigurationResponse(CrossInstanceConfiguration CrossInstanceConfiguration, ErrorInfo? RequestError = null) : IResponse;
