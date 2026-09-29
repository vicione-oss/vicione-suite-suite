using Sdk.Messaging;

namespace Core.Shared.UserManagement.Requests;

/// <summary>
/// <see cref="ClientSecretStored"/> stands in for the client secret, which never leaves the server.
/// </summary>
public sealed record GetExternalIdProviderResponse(
    string Authority,
    string ClientId,
    bool ClientSecretStored,
    ErrorInfo? RequestError = null) : IResponse;
