using System.Diagnostics;
using Sdk.Modules;

namespace Core.Shared.Modules.Contracts;

[DebuggerDisplay("Operation = {OperationKind,nq}, Package = {Package.Name,nq}, Version = {Package.Version,nq}")]
public record ModulePackageOperation(ModuleDependencyPackage Package, ModulePackageOperationKind OperationKind)
{
    /// <summary>
    /// Options are only available if the operation is an update of an already installed package. 
    /// If <see langword="null" />, the operation is either uninstall or a new installation.
    /// </summary>
    public ModulePackageOperationOptions? Options { get; set; }
}
