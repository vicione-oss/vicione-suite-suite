using Core.Shared.Modules.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Modules.Requests;

public sealed record GetModuleMetadataBundlesResponse(List<ModuleMetadataBundle> Bundles, bool PreReleasesAllowed, ErrorInfo? RequestError = null) : IResponse;

