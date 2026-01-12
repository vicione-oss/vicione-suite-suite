using Semver;

namespace Core.Module.Comparer;

public class StringVersionComparer : IComparer<string>
{
    /// <inheritdoc/>
    public int Compare(string? x, string? y)
    {
        var xParsed = SemVersion.TryParse(x, out var xParsedVersion);
        var yParsed = SemVersion.TryParse(y, out var yParsedVersion);

        if (!xParsed && !yParsed)
            return 0;

        if (xParsed && !yParsed)
            return 1;

        if (!xParsed && yParsed)
            return -1;

        return SemVersion.CompareSortOrder(xParsedVersion, yParsedVersion);
    }
}
