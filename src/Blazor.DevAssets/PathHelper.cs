using System.Diagnostics;

namespace Blazor.DevAssets;

internal static class PathHelper
{
    private const string PARENT_DIR_DOUBLEDOT = "..";

#pragma warning disable IDE1006 // Naming Styles
    private static readonly char DIR_SEPARATOR_CHAR = Path.DirectorySeparatorChar;
    private static readonly string DIR_SEPARATOR_STRING = Path.DirectorySeparatorChar.ToString();
    private static readonly string TWO_DIR_SEPARATOR_STRING = Path.DirectorySeparatorChar + Path.DirectorySeparatorChar.ToString();
#pragma warning restore IDE1006 // Naming Styles

    internal static string NormalizePath(string path)
    {
        Debug.Assert(path is not null);
        Debug.Assert(path.Length > 0);
        path = path.Replace('/', DIR_SEPARATOR_CHAR);
        path = path.TrimEnd(); // Trailing spaces only, not leading.

        if (path.StartsWith(TWO_DIR_SEPARATOR_STRING, StringComparison.Ordinal))
        {
            // A leading double separator can be a UNC path, so keep two. Dropping one before collapsing
            // the rest leaves exactly two when the path starts with three or more.
            var pathTmp = path[1..];
            pathTmp = EventuallyRemoveConsecutiveSeparator(pathTmp);
            path = DIR_SEPARATOR_STRING + pathTmp;
        }
        else
        {
            path = EventuallyRemoveConsecutiveSeparator(path);
        }

        // Turn a ".\.." prefix into "..".
        const string PREFIX_TO_SIMPLIFY = @".\..";
        if (path.StartsWith(PREFIX_TO_SIMPLIFY, StringComparison.Ordinal))
        {
            path = path.Remove(0, PREFIX_TO_SIMPLIFY.Length);
            path = path.Insert(0, PARENT_DIR_DOUBLEDOT);
        }

        // Strip trailing separators.
        while (true)
        {
            var pathLength = path.Length;
            if (pathLength == 0)
                return "";

            var pathLengthMinusOne = pathLength - 1;
            var lastChar = path[pathLengthMinusOne];
            if (lastChar != DIR_SEPARATOR_CHAR)
                break;
            path = path[..pathLengthMinusOne];
        }
        return path;
    }

    private static string EventuallyRemoveConsecutiveSeparator(string path)
    {
        Debug.Assert(path is not null);
        while (path.Contains(TWO_DIR_SEPARATOR_STRING, StringComparison.Ordinal))
        {
            path = path.Replace(TWO_DIR_SEPARATOR_STRING, DIR_SEPARATOR_STRING, StringComparison.Ordinal);
        }
        return path;
    }

    internal static bool HasParentDirectory(string path)
    {
        Debug.Assert(path is not null);
        return path.Contains(DIR_SEPARATOR_STRING, StringComparison.Ordinal);
    }

    internal static string GetParentDirectory(string path)
    {
        Debug.Assert(path is not null);
        if (!HasParentDirectory(path))
            throw new InvalidOperationException(@"Can't get the parent dir from the pathString """ + path + @"""");
        var index = path.LastIndexOf(DIR_SEPARATOR_CHAR);
        Debug.Assert(index >= 0);
        return path[..index];
    }

    internal static string GetLastName(string path)
    {
        Debug.Assert(path is not null);
        if (!HasParentDirectory(path))
            // Directories like "." or "C:" have no last name.
            return "";
        var index = path.LastIndexOf(DIR_SEPARATOR_CHAR);
        Debug.Assert(index != path.Length - 1);
        return path.Substring(index + 1, path.Length - index - 1);
    }
}
