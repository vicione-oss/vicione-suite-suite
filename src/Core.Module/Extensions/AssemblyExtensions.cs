using System.Reflection;

namespace Core.Module.Extensions;

internal static class AssemblyExtensions
{
    public static bool IsSuiteModule(this Assembly moduleAssembly, string fullName)
    {
        foreach (var type in moduleAssembly.GetTypes())
        {
            if (!HasBaseTypeOfInterest(type))
                continue;

            if (Equals(type.BaseType!.FullName, fullName))
            {
                return true;
            }
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
            // Thrown when an unknown type is accessed.
            return false;
        }

        return true;
    }
}
