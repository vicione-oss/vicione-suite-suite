using System.Reflection;
using Core.Module.Exceptions;
using Core.Module.Utils;
using Microsoft.Extensions.DependencyModel;
using Semver;

namespace Core.Module.Extensions;

public static class ModuleDependencyContextExtensions
{
    public static bool HasRuntimeLibrary(this ModuleDependencyContext context, string name, string version)
        => context.RuntimeLibraries.Any(k => k.IsMatch(name, version));

    public static RuntimeLibrary GetMainLibrary(this ModuleDependencyContext context) =>
        context.RuntimeLibraries.FirstOrDefault(x => x.Name == context.AssemblyName)
        ?? throw new KeyNotFoundException(context.AssemblyName);

    /// <summary>
    /// Returns first available sdk library version that is referenced by the module context runtime libraries.
    /// </summary>
    /// <returns>{major}.{minor}.{patch}</returns>
    public static string GetSdkVersion(this ModuleDependencyContext context)
    {
        var sdkAssembly = ModuleHelpers.GetSdkAssemblyName();
        var sdkName = sdkAssembly.Name ?? throw new InvalidOperationException("Sdk assembly name is null");
        var sdkLibs = context.RuntimeLibraries.Where(x => x.Name.StartsWith(sdkName, StringComparison.Ordinal))
                .Concat(context.RedundantLibraries.Where(x => x.Name.StartsWith(sdkName, StringComparison.Ordinal)));

        var sdkVersions = sdkLibs.Select(x => x.Version).Distinct().ToList();
        if (sdkVersions.Count > 1)
            throw new SdkIncompatibilityException("SDK versions mismatch");

        if (sdkVersions.Count == 0)
            throw new SdkIncompatibilityException("No referenced SDK version found");

        // if we use a sdk pre-release version we have e.g. 0.20.0-ci1447880        
        if (!SemVersion.TryParse(sdkVersions.First(), out var version))
            throw new SdkIncompatibilityException("Referenced SDK version is invalid");

        // .. -> 0.20.0
        return ModuleHelpers.GetNormalizedVersion(version);
    }

    /// <summary>
    /// returns direct project dependencies of main like *.Internal|Public
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IEnumerable<Dependency> GetModuleDependencies(this ModuleDependencyContext context)
    {
        var moduleRt = context.GetMainLibrary();

        // filter by naming convention e.g. ViciOne.Suite.XXX[.Internal|.Public]
        if (ModuleHelpers.TryGetFamilyNamePart(moduleRt.Name, out var commonNamePart))
        {
            var deps = moduleRt.Dependencies.SelectMany(context.GetModuleDependenciesInternal).Concat(moduleRt.Dependencies);
            return deps.Where(k => k.Name.StartsWith(commonNamePart, StringComparison.Ordinal));
        }

        return [];
    }

    /// <summary>
    /// gets all *.Public dependencies from other modules
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IEnumerable<Dependency> GetExternalPublicDependencies(this ModuleDependencyContext context)
    {
        var moduleRt = context.GetMainLibrary();

        // filter by naming convention e.g. ViciOne.Suite.XXX[.Internal|.Public]
        if (!ModuleHelpers.TryGetFamilyNamePart(moduleRt.Name, out var commonNamePart))
            return [];

        var deps = moduleRt.Dependencies
            .SelectMany(context.GetModuleDependenciesInternal)
            .Concat(moduleRt.Dependencies);

        return deps.Where(k => !k.Name.StartsWith(commonNamePart, StringComparison.Ordinal)
            && k.Name.EndsWith(Constants.ModuleSuffixPublic, StringComparison.Ordinal));
    }

    private static IEnumerable<Dependency> GetModuleDependenciesInternal(this ModuleDependencyContext context, Dependency dependency)
    {
        var depRt = context.RuntimeLibraries.FirstOrDefault(k => k.Name == dependency.Name && k.Version == dependency.Version);
        if (depRt is null)
            return [];

        return [.. depRt.Dependencies];
    }

    public static bool HasRuntimeAssembly(this ModuleDependencyContext context, AssemblyName assemblyName) => context.GetRuntimeLibrary(assemblyName) is not null;

    private static RuntimeLibrary? GetRuntimeLibrary(this ModuleDependencyContext context, AssemblyName assemblyName)
        => string.IsNullOrEmpty(assemblyName.Name) ? null : context.GetRuntimeLibrary(assemblyName.Name, assemblyName.Version);

    public static bool HasDirectDependency(this ModuleDependencyContext context, AssemblyName assemblyName)
        => context.GetDirectDependency(assemblyName.Name, assemblyName.Version) is not null;

    public static IEnumerable<string> GetRedundantLibraryPaths(this ModuleDependencyContext moduleContext, Func<string, string, bool>? filter = null)
    {
        var infos = new List<string>();
        var depsFolder = Path.GetDirectoryName(moduleContext.DepsJsonFilePath);
        if (string.IsNullOrEmpty(depsFolder))
            return infos;

        foreach (var excludedLibrary in moduleContext.RedundantLibraries)
        {
            if (filter is not null && filter.Invoke(excludedLibrary.Name, excludedLibrary.Version))
                continue;

            var dllName = moduleContext.GetDefaultAssemblyName(excludedLibrary.Name, excludedLibrary.Version);
            if (string.IsNullOrEmpty(dllName))
                continue;

            var dllPath = Path.Combine(depsFolder, dllName);
            if (File.Exists(dllPath))
                infos.Add(dllPath);

            var pdbPath = Path.Combine(depsFolder, $"{Path.GetFileNameWithoutExtension(dllName)}.pdb");
            if (File.Exists(pdbPath))
                infos.Add(pdbPath);
        }

        return infos;
    }

    internal static bool IsGoodAssembly(this ModuleDependencyContext c, string name, string version, out string assemblyPath)
    {
        assemblyPath = string.Empty;
        var assemblyName = c.GetDefaultAssemblyName(name, version);
        if (string.IsNullOrEmpty(assemblyName)) return false;

        assemblyPath = Path.Combine(c.AssemblyFolder, assemblyName);
        return File.Exists(assemblyPath);
    }

    /// <summary>
    /// gets all contexts referenced by moduleContext  
    /// </summary>
    /// <param name="contexts"></param>
    /// <param name="moduleContext"></param>
    /// <returns></returns>
    internal static IEnumerable<ModuleDependencyContext> GetDependencyContexts(this List<ModuleDependencyContext> contexts, ModuleDependencyContext moduleContext)
    {
        var moduleRt = moduleContext.GetMainLibrary();
        var ret = new List<ModuleDependencyContext>();

        foreach (var dependency in moduleRt.Dependencies)
        {
            // in: ViciOne.Suite.XXX.Public out: ViciOne.Suite.XXX  
            if (!ModuleHelpers.TryGetFamilyNamePart(dependency.Name, out var commonName))
                continue;

            // exclude dependencies from same module like ViciOne.Suite.XXX.Internal|Public
            if (moduleRt.Name.StartsWith(commonName, StringComparison.Ordinal))
                continue;

            // now try to add all contexts that belong to ViciOne.Suite.XXX
            ret.AddRange(contexts.Where(k => k.AssemblyName.StartsWith(commonName, StringComparison.Ordinal)));
        }

        return ret;
    }
}
