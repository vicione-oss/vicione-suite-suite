using Core.Shared.Modules.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Modules.Requests;

/// <param name="Bundles">The requested module bundles.</param>
/// <param name="RequestError">Set when the bundles could not be determined.</param>
/// <param name="AllowInstallation">Whether the instance lets the user install, update and uninstall modules.</param>
public sealed record GetModuleMetadataBundlesResponse(List<ModuleMetadataBundle> Bundles, ErrorInfo? RequestError = null, bool AllowInstallation = false)
    : IResponse;

