using System.Collections;

namespace Core.Module;

internal sealed class ModuleDependencyContextComparer(SuiteDependencyContext suiteDependency) :
    IComparer, IComparer<ModuleDependencyContext>
{
    private readonly SuiteDependencyContext _suiteContext = suiteDependency;

    public int Compare(object? x, object? y)
        => Compare(x as ModuleDependencyContext, y as ModuleDependencyContext);

    public int Compare(ModuleDependencyContext? x, ModuleDependencyContext? y)
    {
        if (x == null && y == null)
            return 0;

        if (x != null && y == null)
            return 1;

        if (x == null && y != null)
            return -1;

        // now use the mappings to determine the dependencies
        var xMap = _suiteContext.Mappings.FirstOrDefault(k => k.MapTo.Module == x!.AssemblyName);
        var yMap = _suiteContext.Mappings.FirstOrDefault(k => k.MapTo.Module == y!.AssemblyName);

        if (xMap == null && yMap == null)
            return 0;

        if (xMap != null && yMap == null)
            return 1;

        if (xMap == null && yMap != null)
            return -1;

        // both have mappings from others
        if (xMap!.MapFrom.Any(k => k.Module == y!.AssemblyName))
            return 1;

        if (yMap!.MapFrom.Any(k => k.Module == x!.AssemblyName))
            return -1;

        return 0;
    }
}
