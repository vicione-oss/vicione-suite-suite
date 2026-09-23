using System.Diagnostics;
using Sdk.Modules;

namespace Core.Shared.Modules.Contracts;

[DebuggerDisplay("Operation = {OperationKind,nq}, Package = {Package.Name,nq}, Version = {Package.Version,nq}")]
public record ModulePackageOperation(ModuleDependencyPackage Package, ModulePackageOperationKind OperationKind)
{
    /// <summary>
    /// Set only for an update of an installed package; <see langword="null"/> for install and uninstall.
    /// </summary>
    public ModulePackageOperationOptions? Options { get; set; }
}
