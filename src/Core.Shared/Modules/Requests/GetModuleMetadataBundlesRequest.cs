using Sdk.Messaging;

namespace Core.Shared.Modules.Requests;

public sealed record GetModuleMetadataBundlesRequest(bool Installed, bool Available, bool ForceRefresh = false)
    : IRequest<GetModuleMetadataBundlesResponse>;
