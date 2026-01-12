namespace Core.Module.Comparer;

public class StringVersionComparer : IComparer<string>
{
    /// <inheritdoc/>
    public int Compare(string? x, string? y)
    {
        if (x == null && y == null)
            return 0;

        if (x != null && y == null)
            return 1;

        if (x == null && y != null)
            return -1;

        if (MetadataAssetVersionRegex.GetVersions(x, out var xVersion, out var xCi) &&
            Version.TryParse(xVersion, out var xParsed))
        {
            if (MetadataAssetVersionRegex.GetVersions(y, out var yVersion, out var yCi) &&
                Version.TryParse(yVersion, out var yParsed))
            {
                var cmp = xParsed.CompareTo(yParsed);
                if (cmp == 0)
                {
                    if (string.IsNullOrEmpty(xCi) && string.IsNullOrEmpty(yCi))
                        return 0;

                    // e.g. released version 0.2.4 is higher than 0.2.4-ci123423
                    if (string.IsNullOrEmpty(xCi))
                        return 1;

                    if (string.IsNullOrEmpty(yCi))
                        return -1;

                    // ci223423 is greater than ci123423
                    // improve by compare only the pipeline numbers?
                    return string.CompareOrdinal(xCi, yCi);
                }

                return cmp;
            }

            return 1;
        }

        {
            if (MetadataAssetVersionRegex.GetVersions(y, out var yVersion, out var _) &&
                Version.TryParse(yVersion, out var _))
                return -1;
        }

        return 0;
    }
}
