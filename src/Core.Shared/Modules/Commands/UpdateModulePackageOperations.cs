using Core.Shared.Modules.Contracts;
using Sdk.Messaging;

namespace Core.Shared.Modules.Commands;

public sealed record UpdateModulePackageOperations(List<ModulePackageOperation> Operations) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
