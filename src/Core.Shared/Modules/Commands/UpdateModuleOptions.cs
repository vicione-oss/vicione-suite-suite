using Sdk.Messaging;
using Sdk.Modules;

namespace Core.Shared.Modules.Commands;

public sealed record UpdateModuleOptions(string ModuleId, List<ModuleOptionDeclaration> Options) : ICommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
