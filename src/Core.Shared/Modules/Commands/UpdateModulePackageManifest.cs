using Sdk.Messaging;
using Sdk.Modules;

namespace Core.Shared.Modules.Commands;

[Obsolete("Use UpdateModulePackages instead")]
public sealed record UpdateModulePackageManifest(ModulePackageManifest Manifest) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
