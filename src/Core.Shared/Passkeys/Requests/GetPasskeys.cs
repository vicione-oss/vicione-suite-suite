using Sdk.Messaging;

namespace Core.Shared.Passkeys.Requests;

public sealed record GetPasskeys(string UserId) : IRequest<GetPasskeysResponse>;
