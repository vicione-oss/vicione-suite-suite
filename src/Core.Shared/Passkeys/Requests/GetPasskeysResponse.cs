using Core.Shared.Passkeys.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Passkeys.Requests;

public sealed record GetPasskeysResponse(List<PasskeyInfo> Passkeys, ErrorInfo? RequestError = null) : IResponse;
