using Sdk.Messaging;
using Sdk.Modules;

namespace Core.Shared.Modules.Commands;

public sealed record UpdateModulePackageManifest(ModulePackageManifest Manifest) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
