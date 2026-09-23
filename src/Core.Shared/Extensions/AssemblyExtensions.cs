using System.Reflection;

namespace Core.Shared.Extensions;

public static class AssemblyExtensions
{
    public static bool IsSuiteModule(this Assembly moduleAssembly, string fullName, Func<string?, bool>? skipAssemblyFilter = null)
    {
        try
        {
            foreach (var type in moduleAssembly.GetTypes())
            {
                if (!HasBaseTypeOfInterest(type))
                    continue;

                if (Equals(type.BaseType!.FullName, fullName))
                {
                    // The Blazor server UI host carries a client module inside its backend assembly; until
                    // that is resolved, the naming convention has to filter it out.
                    if (skipAssemblyFilter is not null && skipAssemblyFilter.Invoke(moduleAssembly.GetName().Name))
                        return false;

                    return true;
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // On linux this throws:
            // Could not load type 'WebAssembly.JSInterop.JSCallInfo' from assembly 'Microsoft.JSInterop.WebAssembly, Version=6.0.5.0, Culture=neutral, PublicKeyToken=adb9793829ddae60'
            // because it contains an object field at offset 4 that is incorrectly aligned or overlapped by a non-object field.
            return false;
        }

        return false;
    }

    private static bool HasBaseTypeOfInterest(Type type)
    {
        try
        {
            // Accessing BaseType triggers an assembly load; an unresolvable type throws FileNotFoundException.
            if (type.BaseType is null)
                return false;
        }
        catch (FileNotFoundException)
        {
            // Thrown when an unknown type is accessed.
            return false;
        }

        return true;
    }
}
