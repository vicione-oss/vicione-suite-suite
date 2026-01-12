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
                    // there's the special case that blazor server ui host has a client module within the backend assembly
                    // till we have a better solution the naming convention has to filter it out
                    if (skipAssemblyFilter is not null && skipAssemblyFilter.Invoke(moduleAssembly.GetName().Name))
                        return false;

                    return true;
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // On linux test: 
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
            // Accessing the BaseType will trigger reflection to load the assembly of the type.
            // For any type that can't be resolved we'll catch the FileNotFoundException and ignore it
            if (type.BaseType is null)
                return false;
        }
        catch (FileNotFoundException)
        {
            // these are thrown if we access an unknown type - we don't care about them!
            return false;
        }

        return true;
    }
}
