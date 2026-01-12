using Sdk.Client.Modules;
using Sdk.Modules;

namespace Blazor.Shared;

/// <summary>
/// Module implementing client functionality in shared scope.
/// 
/// The class is implementing <see cref="IClientModule"/> instead of inheriting from base class <see cref="ClientModule"/> to
/// hide the class from ModuleFinder. This is needed to ensure the Shared assembly is not recognized as an actual UI module.
/// </summary>
public sealed class SharedClientModule : IClientModule
{
    /// <remarks>
    /// Use of predefined SDK constant as SharedClientModule is not an actual module, therefore it cannot have its own ID as
    /// this value would not be known by other application parts incorporating module IDs like authorization.
    /// </remarks>
    public const string ModuleId = Sdk.Constants.SystemModuleId;

    public ModuleKey ModuleKey => new() { ModuleId = ModuleId, ModuleType = ModuleType.Client };

    public IEnumerable<ModuleKey> Dependencies => [];
}
