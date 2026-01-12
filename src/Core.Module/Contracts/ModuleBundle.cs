using System.Diagnostics;
using System.Reflection;
using Sdk.Modules;

namespace Core.Module.Contracts;

[DebuggerDisplay("ModuleId = {Module.ModuleKey.ModuleId,nq}, Type = {Module.ModuleKey.ModuleType,nq}")]
public sealed class ModuleBundle<TModule>(TModule module, Assembly assembly, string assemblyLocation) where TModule : IModule
{
    public TModule Module { get; } = module;

    /// <summary>
    /// needed to let mediator register contained handlers
    /// </summary>
    public Assembly Assembly { get; } = assembly;

    public string AssemblyLocation { get; } = assemblyLocation;
}
