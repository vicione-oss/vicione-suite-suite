using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Core.Module.Extensions;

namespace Core.Module;

internal sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    private readonly SuiteDependencyContext _suiteContext;
    private readonly string _mainAssemblyPath;
    private readonly Stack<IntPtr> _unmanagedAssemblies = new();
    private readonly HashSet<string> _alreadyLoadedFromName = [];

    private AssemblyDependencyResolver Resolver { get; }

    private ModuleAssemblyLoadContext(SuiteDependencyContext suiteContext, string assemblyPath) :
        base(Path.GetFileNameWithoutExtension(assemblyPath))
    {
        _mainAssemblyPath = assemblyPath;
        Resolver = new AssemblyDependencyResolver(assemblyPath);
        _suiteContext = suiteContext;
        Unloading += _ =>
        {
            while (_unmanagedAssemblies.Count != 0)
                NativeLibrary.Free(_unmanagedAssemblies.Pop());
        };
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Returning null hands the assembly to the default resolver.
        if (assemblyName.Name is null)
            return null;

        // The suite is published self-contained and ships many .NET Core assemblies it does not reference.
        if (_suiteContext.Core.RuntimeFiles.Any(k => assemblyName.Name == Path.GetFileNameWithoutExtension(k.Path)))
            return null;

#if DEBUG        
        // Since .net10 the behavior of loading transitive assemblies has changed.
        // If an assembly is already loaded in the default context it will not be resolved here.
        if (assemblyName.Name == "Microsoft.AspNetCore.Components.Forms" || assemblyName.Name == "Microsoft.Extensions.Localization")
        {
            return null;
        }

        if (Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name))
        {
            return null;
        }
#endif

        // An assembly one module provides and another requests is resolved from the owning context.
        var mapping = _suiteContext.Mappings.FirstOrDefault(k => k.AssemblyName == assemblyName.Name);
        if (mapping is not null && mapping.MapTo.Module != Name)
        {
            // e.g. Mediator, Sdk etc.
            if (mapping.MapTo.Module == _suiteContext.Core.AssemblyName)
            {
                // The default context would resolve an exact version, so the already loaded one is reused:
                // a request for Ms.Ef.Relational 8.0.6 has to accept the 8.0.5 the suite provides.
                return Default.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
            }

            var context = _suiteContext.GetContextByName(mapping.MapTo.Module);
            var assembly = LoadAssemblyByDependencyContext(_suiteContext, context, assemblyName);
            if (assembly is not null)
                return assembly;
        }

        // The suite may have loaded it already.
        if (Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name))
        {
            return null;
        }

        var loadedAssembly = Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        // The normal path: resolve through the main deps.json.
        var resolvedPath = Resolver.ResolveAssemblyToPath(assemblyName);
        if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
        {
            // An unmapped assembly referenced by a single module, e.g. DevExpress referencing
            // DevExpress.Drawing.v24.2.Skia directly.
            var moduleContext = _suiteContext.ResolveModuleContext(assemblyName);
            if (moduleContext is not null && moduleContext.DepsJsonFilePath != _mainAssemblyPath)
            {
                // The owning context resolves it.
                var assembly = LoadAssemblyByDependencyContext(_suiteContext, moduleContext, assemblyName);
                if (assembly is not null)
                    return assembly;
            }

            return null;
        }

        return LoadAssemblyFromFilePath(resolvedPath);
    }

    private Assembly? LoadAssemblyByDependencyContext(SuiteDependencyContext suiteContext, ModuleDependencyContext? sourceContext, AssemblyName assemblyName)
    {
        if (sourceContext is null || assemblyName.Name is null)
            return null;

        var contextName = Path.GetFileNameWithoutExtension(sourceContext.AssemblyPath);
        var context = All.FirstOrDefault(c => c.Name == contextName);
        if (context is null)
        {
            // A backend can request a dll from a client whose context is not loaded yet, so the context is
            // created here and used to resolve the assembly path.
            var modContext = Create(suiteContext, sourceContext.AssemblyPath);
            var path = modContext.Resolver.ResolveAssemblyToPath(assemblyName);
            if (string.IsNullOrEmpty(path))
                return null;

            // Loaded into the modContext so it can be reused later.
            return modContext.LoadAssemblyFromFilePath(path);
        }

        // An already loaded assembly, if there is one.
        var assembly = context.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (assembly is not null)
        {
            return assembly;
        }

        try
        {
            // Prevents the repeated load attempts seen on arm64 tests.
            if (_alreadyLoadedFromName.Contains(assemblyName.Name))
                return null;

            _alreadyLoadedFromName.Add(assemblyName.Name);

            // The context is known to hold it.
            return context.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException ex)
        {
            throw new InvalidOperationException($"Context {context.Name} failed to load assembly from name", ex);
        }
    }

    private string? ResolveUnmanagedDllToPath(string unmanagedDllName) => Resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = ResolveUnmanagedDllToPath(unmanagedDllName);

        if (path is not null)
        {
            var pointer = LoadUnmanagedDllFromPath(path);
            _unmanagedAssemblies.Push(pointer);
            return pointer;
        }

        return IntPtr.Zero;
    }

    // todo: unify usage and ModuleAssemblyLoadResult for logging
    internal static ModuleAssemblyLoadContext Create(SuiteDependencyContext suiteContext, string assemblyPath)
    {
        // The context name has to be unique.
        var contextName = Path.GetFileNameWithoutExtension(assemblyPath);
        var existingContext = All.FirstOrDefault(a => a.Name == contextName) as ModuleAssemblyLoadContext;
        if (existingContext is not null)
            return existingContext;

        var context = new ModuleAssemblyLoadContext(suiteContext, assemblyPath);

        // Loads the main assembly into the context.
        context.LoadAssemblyFromFilePath(assemblyPath);

        return context;
    }

    private Assembly LoadAssemblyFromFilePath(string path)
    {
        using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
#if DEBUG
        var pdbPath = Path.ChangeExtension(path, ".pdb");
        if (File.Exists(pdbPath))
        {
            using var pdbFile = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return LoadFromStream(file, pdbFile);
        }
#endif
        return LoadFromStream(file);
    }

    internal Assembly GetMainAssembly()
    {
        var name = Path.GetFileNameWithoutExtension(_mainAssemblyPath);

        var assembly = Assemblies.FirstOrDefault(k => name.Contains(k.GetName().Name!, StringComparison.Ordinal));
        return assembly is null ? throw new FileLoadException(name) : assembly;
    }

    internal string GetMainAssemblyPath() => _mainAssemblyPath;
}
