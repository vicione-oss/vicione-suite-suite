using Sdk.Messaging;

namespace Core.Shared.Modules.Requests;

/// <param name="Installed">Returns the bundles of installed modules.</param>
/// <param name="Available">Returns the bundles of modules that are available but not installed.</param>
/// <param name="ForceRefresh">Bypasses the artifact cache when the repository is queried.</param>
/// <param name="ResolveUpdates">Queries the repository for the versions of installed modules without returning available ones.</param>
public sealed record GetModuleMetadataBundlesRequest(bool Installed, bool Available, bool ForceRefresh = false, bool ResolveUpdates = false)
    : IRequest<GetModuleMetadataBundlesResponse>;
