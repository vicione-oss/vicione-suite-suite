using Core.Shared.Modules.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Modules.Requests;

public sealed record GetModuleMetadataBundlesResponse(List<ModuleMetadataBundle> Bundles, ErrorInfo? RequestError = null) : IResponse;

