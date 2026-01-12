namespace Core.Module;

public sealed record SuiteDependencyContext(
    ModuleDependencyContext Core,
    List<ModuleDependencyContext> Modules,
    List<ModuleDependencyContext> UiHosts)
{
    public List<ModuleAssemblyMapping> Mappings { get; } = [];

    public bool AreDependenciesValidated { get; set; }
}

public sealed record AssetLibrary(string Name, string Path);

public record ModuleAssemblyMapping(string AssemblyName, ModuleMappingVersion MapTo)
{
    public required List<ModuleMappingVersion> MapFrom { get; init; }
};

public record ModuleMappingVersion(string Module, string Version);
