using System.Reflection;

namespace Core.Module;

public sealed class ModuleMetadataLoadContext : IDisposable
{
    private readonly MetadataLoadContext _context;

    public IReadOnlyList<Assembly> Assemblies => _context.GetAssemblies().ToList();

    public ModuleMetadataLoadContext(IEnumerable<string> components)
    {
        var coreAssembly = typeof(int).Assembly;
        _context = new MetadataLoadContext(new ModuleMetadataAssemblyResolver(components, [coreAssembly.Location,]), coreAssembly.FullName);
    }

    public void LoadAssemblies(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            _context.LoadWithStreamFromPath(path);
    }

    public void Dispose() => _context.Dispose();
}
