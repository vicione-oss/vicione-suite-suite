using Sdk.Messaging;

namespace Core.Shared.Modules.Requests;

public sealed record GetModuleMetadataBundlesRequest(bool Installed, bool Available, Version? SdkVersion = null, bool IncludePreReleases = false, bool ForceRefresh = false)
    : IRequest<GetModuleMetadataBundlesResponse>;
