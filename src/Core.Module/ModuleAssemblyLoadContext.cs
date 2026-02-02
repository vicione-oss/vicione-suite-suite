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
        // if we return null we let the default resolver handle the libs
        if (assemblyName.Name is null)
            return null; // not sure how to handle this case. It's technically possible.

        // suite is published self-contained and ships many NET.Core assemblies it does not directly reference
        if (_suiteContext.Core.RuntimeFiles.Any(k => assemblyName.Name == Path.GetFileNameWithoutExtension(k.Path)))
            return null;

#if DEBUG        
        // Since .net10 the behavior of loading transitive assemblies has changed.
        // If an assembly is already loaded in the default context it will not be resolved here.
        if (Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name))
        {
            return null;
        }
#endif

        // for all assemblies provided by a module requested by another one we have a mapping to resolve it from the right context
        var mapping = _suiteContext.Mappings.FirstOrDefault(k => k.AssemblyName == assemblyName.Name);
        if (mapping is not null && mapping.MapTo.Module != Name)
        {
            // e.g. Mediator, Sdk etc.
            if (mapping.MapTo.Module == _suiteContext.Core.AssemblyName)
            {
                // here our source is the default context that would resolve to exact assembly version                 
                // if Ms.Ef.Relational in 8.0.6 is requested but suite provides 8.0.5 we need to reuse the loaded 
                return Default.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
            }

            var context = _suiteContext.GetContextByName(mapping.MapTo.Module);
            var assembly = LoadAssemblyByDependencyContext(_suiteContext, context, assemblyName);
            if (assembly is not null)
                return assembly;
        }

        // maybe the assembly was already loaded by suite
        if (Default.Assemblies.Any(a => a.GetName().Name == assemblyName.Name))
        {
            return null;
        }

        // try to get already loaded assembly from this context
        var loadedAssembly = Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        // resolve using main deps json - the normal way
        var resolvedPath = Resolver.ResolveAssemblyToPath(assemblyName);
        if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
        {
            // here we have a situation where an assembly is requested that is not mapped.
            // this affects assemblies referenced by a single module like DevExpress with a direct
            // reference to e.g. DevExpress.Drawing.v24.2.Skia
            var moduleContext = _suiteContext.ResolveModuleContext(assemblyName);
            if (moduleContext is not null && moduleContext.DepsJsonFilePath != _mainAssemblyPath)
            {
                // now we have the context that owns the assembly so we let the module context resolve it
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
            // it's possible that backend requests a dll from client but it's context is not yet loaded
            // therefore we need to create it and use it to resolve the assembly path.            
            var modContext = Create(suiteContext, sourceContext.AssemblyPath);
            var path = modContext.Resolver.ResolveAssemblyToPath(assemblyName);
            if (string.IsNullOrEmpty(path))
                return null;

            // the assembly gets loaded into the modContext to be reused later on
            return modContext.LoadAssemblyFromFilePath(path);
        }

        // try to get already loaded assembly
        var assembly = context.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (assembly is not null)
        {
            //Debug.WriteLine("ModuleAssemblyLoadContext[{0}]:: {1} is shared. use existing requested by {2}", context.Name, assemblyName, _mainAssemblyPath);
            return assembly;
        }

        //Debug.WriteLine("ModuleAssemblyLoadContext[{0}]:: {1} is shared. load it directly requested by {2}", context.Name, assemblyName, _mainAssemblyPath);
        try
        {
            // prevent multiple load attempts seen on arm64 tests
            if (_alreadyLoadedFromName.Contains(assemblyName.Name))
                return null;

            _alreadyLoadedFromName.Add(assemblyName.Name);

            // we know we have it!
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
        // ensure the context name is unique
        var contextName = Path.GetFileNameWithoutExtension(assemblyPath);
        var existingContext = All.FirstOrDefault(a => a.Name == contextName) as ModuleAssemblyLoadContext;
        if (existingContext is not null)
            return existingContext;

        var context = new ModuleAssemblyLoadContext(suiteContext, assemblyPath);

        // load the main assembly into the context
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
