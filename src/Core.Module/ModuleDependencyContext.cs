using Core.Module.Extensions;
using Core.Module.Utils;
using Microsoft.Extensions.DependencyModel;
using Sdk.Modules;

namespace Core.Module;

public sealed class ModuleDependencyContext
{
    private const string RuntimePackTypeIdentifier = "runtimepack";

    /// <summary>
    /// this mapper contains assemblies where the assembly has a different name as the library itself
    /// Z.Blazor.Diagrams -> Blazor.Diagrams.dll
    /// </summary>
    private readonly Dictionary<string, string> _libraryToNameMap = [];

    private readonly Dictionary<string, string> _nameToLibraryMap = [];

    public ModuleType ModuleType { get; }

    public string ModuleId { get; init; }

    /// <summary>
    /// the json containing all dependencies of the module
    /// </summary>
    public string DepsJsonFilePath { get; }

    public bool IsDebugSource { get; }
    public string AssemblyPath { get; }
    public string AssemblyFolder { get; }
    public string AssemblyName { get; }
    public List<RuntimeLibrary> RuntimeLibraries { get; } = [];
    public List<RuntimeLibrary> RedundantLibraries { get; } = [];
    public List<AssetLibrary> RuntimeAssets { get; } = [];
    public List<AssetLibrary> RedundantAssets { get; } = [];
    public List<RuntimeFile> RuntimeFiles { get; } = [];
    public List<Exception> StartupErrors { get; } = [];

#if DEBUG// quite useful on debugging :)
    public List<string> RuntimeLibraryKeys => RuntimeLibraries
        .Select(k => ModuleHelpers.GetNameVersionKey(k.Name, k.Version))
        .ToList();

    public List<string> RedundantLibraryKeys => RedundantLibraries
        .Select(k => ModuleHelpers.GetNameVersionKey(k.Name, k.Version))
        .ToList();

    public List<string> LibraryToNameMap => _libraryToNameMap
        .Select(k => $"{k.Key} -> {k.Value}")
        .ToList();

#endif

    public ModuleDependencyContext(ModuleType moduleType, string depsJsonFilePath, bool isDebugSource)
    {
        DepsJsonFilePath = depsJsonFilePath;
        IsDebugSource = isDebugSource;
        AssemblyPath = ModuleHelpers.DepsJsonToDll(DepsJsonFilePath);
        AssemblyName = Path.GetFileNameWithoutExtension(AssemblyPath);
        AssemblyFolder = Path.GetDirectoryName(AssemblyPath) ?? throw new DirectoryNotFoundException();

        // ViciOne.Suite.Module.Backend|Client -> ViciOne.Suite.Module
        ModuleId = GetIdFromModuleName(AssemblyName);
        ModuleType = moduleType;
    }

    public static string GetIdFromModuleName(string moduleName)
    {
        if (moduleName.EndsWith(Constants.ModuleSuffixBackend, StringComparison.Ordinal))
            return moduleName.Replace(Constants.ModuleSuffixBackend, "", StringComparison.Ordinal);

        else if (moduleName.EndsWith(Constants.ModuleSuffixClient, StringComparison.Ordinal))
            return moduleName.Replace(Constants.ModuleSuffixClient, "", StringComparison.Ordinal);

        return moduleName;
    }

    /// <summary>
    /// inits all name mappings for current runtime libraries
    /// e.g. DevExpress.Blazor maps to DevExpress.Blazor.vXX.X
    /// or Z.Blazor.Diagrams maps to Blazor.Diagrams
    /// </summary>
    /// <param name="context"></param>
    internal void AddRuntimeLibraries(DependencyContext context)
    {
        RuntimeLibraries.AddRange(context.RuntimeLibraries);

        foreach (var runtimeLibrary in RuntimeLibraries)
        {
            // if we have files with resources we get multiple names here like
            // ViciOne.Ui.Blazor.Components|ViciOne.Ui.Blazor.Components.Resources
            var names = runtimeLibrary.GetDefaultAssemblyNames(context).OrderBy(k => k.Name);
            var defaultName = names.FirstOrDefault();
            if (defaultName is not null && !string.IsNullOrEmpty(defaultName.Name))
            {
                _nameToLibraryMap.TryAdd(defaultName.Name, ModuleHelpers.GetNameVersionKey(runtimeLibrary.Name, runtimeLibrary.Version));
                _libraryToNameMap.TryAdd(ModuleHelpers.GetNameVersionKey(runtimeLibrary.Name, runtimeLibrary.Version), defaultName.Name);
            }
        }

        // runtimepacks get added to the *.deps.json if it gets published self-contained
        // relevant to let core provide assemblies it does not know by reference
        var runtimepacks = context.RuntimeLibraries.Where(k => k.Type == RuntimePackTypeIdentifier);
        foreach (var runtimepack in runtimepacks)
        {
            foreach (var assemblyGroup in runtimepack.RuntimeAssemblyGroups)
            {
                RuntimeFiles.AddRange(assemblyGroup.RuntimeFiles);
            }
        }
    }

    public RuntimeLibrary? GetRuntimeLibrary(string assemblyName, Version? version)
    {
        // check if we have a name mapping for this assembly
        if (_nameToLibraryMap.TryGetValue(assemblyName, out var match))
            return RuntimeLibraries.FirstOrDefault(k => k.HasNameVersionKey(match));

        // seldom but can happen...
        if (version is null)
            return RuntimeLibraries.FirstOrDefault(k => k.Name == assemblyName);

        // try to get it from rt libraries by name and version
        return RuntimeLibraries.FirstOrDefault(k => k.IsMatch(assemblyName, version.ToString(3)));
    }

    public Dependency? GetDirectDependency(string? assemblyName, Version? version)
        => GetDirectDependency(assemblyName, version?.ToString(3));

    private Dependency? GetDirectDependency(string? assemblyName, string? version)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return null;

        var mainRuntimeLibrary = this.GetMainLibrary();

        // check if we have a name mapping for this assembly
        if (_nameToLibraryMap.TryGetValue(assemblyName, out var match))
            return ReturnNullOrDep(mainRuntimeLibrary.Dependencies.FirstOrDefault(k => k.HasNameVersionKey(match)));

        // can happen...

        if (version is null)
        {
#if DEBUG
            return ReturnNullOrDep(mainRuntimeLibrary.Dependencies.FirstOrDefault(k => k.Name == assemblyName || k.Name == $"{assemblyName}.Reference"));
#else
            return ReturnNullOrDep(mainRuntimeLibrary.Dependencies.FirstOrDefault(k => k.Name == assemblyName));
#endif
        }

        // try to get it from rt libraries by name and version
        return ReturnNullOrDep(mainRuntimeLibrary.Dependencies.FirstOrDefault(k => k.IsMatch(assemblyName, version)));
    }

    private static Dependency? ReturnNullOrDep(Dependency dependency)
    {
        // first or default always returns a dependency - so we need to check ourselves
        if (string.IsNullOrEmpty(dependency.Name) && string.IsNullOrEmpty(dependency.Version))
            return null;

        return dependency;
    }

    public string? GetDefaultAssemblyName(string name, string version, bool addExtension = true)
    {
        var extension = addExtension ? ".dll" : string.Empty;

        if (_libraryToNameMap.TryGetValue(ModuleHelpers.GetNameVersionKey(name, version), out var defaultName))
            return $"{defaultName}{extension}";

        if (RuntimeLibraries.Any(k => k.IsMatch(name, version)))
            return $"{name}{extension}";

        if (RedundantLibraries.Any(k => k.IsMatch(name, version)))
            return $"{name}{extension}";

        return null;
    }

    public override string ToString()
        => $"{AssemblyName} libraries:{RuntimeLibraries.Count} assets={RuntimeAssets.Count}";

    internal void MarkRendundantLibrary(RuntimeLibrary library)
    {
        if (!RuntimeLibraries.Exists(k => k.IsMatch(library.Name, library.Version)))
            return;

        RuntimeLibraries.Remove(library);
        RedundantLibraries.Add(library);

        var libraryKey = ModuleHelpers.GetNameVersionKey(library.Name, library.Version);

        if (_libraryToNameMap.TryGetValue(libraryKey, out var defaultName))
        {
            _libraryToNameMap.Remove(libraryKey);
            _nameToLibraryMap.Remove(defaultName);
        }
    }
}
