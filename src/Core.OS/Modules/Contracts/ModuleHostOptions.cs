using Core.Module;
using Core.Module.Contracts;
using Core.Shared.Modules.Contracts;
using Sdk.Backend.Modules;

namespace Core.OS.Modules.Contracts;

internal class ModuleHostOptions
{
    public required IConfiguration Configuration { get; init; }

    public required Dictionary<string, ModuleOptions> ModuleOptions { get; init; }

    /// <summary>
    /// Containing all assembly reference information created from *.deps.json files
    /// </summary>
    public required SuiteDependencyContext SuiteContext { get; init; }

    /// <summary>
    /// A collection of modules that were loaded into assembly context without errors
    /// </summary>
    public required IEnumerable<ModuleBundle<BackendModule>> LoadedBundles { get; init; }

    /// <summary>
    /// A collection of all modules defined by manifest including modules we failed to load
    /// </summary>
    public required IReadOnlyCollection<ModuleMetadataBundle> Modules { get; init; }

    /// <summary>
    /// Needed on UIHost initialization of modules
    /// </summary>
    public IMvcBuilder? MvcBuilder { get; init; }
}
