using Sdk.Messaging;

namespace Core.Shared.Modules.Contracts;

public record ModulePackageChange(CrudAction Action, ModulePackageOperation Operation, string? PreviousVersion = null);
