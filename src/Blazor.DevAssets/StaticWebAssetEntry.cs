using System.Globalization;
using System.Text.Json;

namespace Blazor.DevAssets;

internal sealed class StaticWebAssetEntry
{
    public string? Key { get; set; }
    public List<StaticWebAssetEntryContent> Assets { get; set; } = [];

    public StaticWebAssetEntry(JsonElement? entry)
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));

        // e.g. Root.Children.js.dashboard-blazor.js
        foreach (var item in entry.Value.EnumerateObject())
        {
            if (item.Name == StaticWebAssetContent.ChildrenPropertyName)
            {
                AddChildrenR(item);
            }

            if (item.Name == StaticWebAssetContent.AssetPropertyName)
            {
                AddEntryContent(item);
            }
        }
    }

    private void AddChildrenR(JsonProperty item)
    {
        if (item.Value.ValueKind == JsonValueKind.Null)
            return;

        foreach (var child in item.Value.EnumerateObject())
        {
            var fileEntry = child.Value.Deserialize<EntryContentWrapper>();
            if (fileEntry?.Asset is null)
            {
                // Recurse to the leaves.
                AddChildrenR(child);
                continue;
            }

            Assets.Add(fileEntry.Asset);
        }
    }

    private void AddEntryContent(JsonProperty item)
    {
        var asset = item.Value.Deserialize<StaticWebAssetEntryContent>();
        if (asset is null)
            return;

        Assets.Add(asset);
    }

    public StaticWebAssetEntryContent? FindAssetContent(string subpath, string fallbackFileName, string lastChance)
    {
        // Match on subpath first, then the filename for module-scoped css, then a parent\file fallback.
        var assets = Assets
            .Where(k => !string.IsNullOrEmpty(k.SubPath) &&
                        (Equals(k.SubPathNormalized, subpath) || Equals(k.SubPath, fallbackFileName) || Equals(k.SubPathNormalized, lastChance)))
            .ToArray();

        if (assets.Length == 0)
            return null;

        if (assets.Length == 1)
            return assets[0];

        // Multiple matches, e.g. dx-blazor-750968d4.js and modules/dx-blazor-750968d4.js. The one whose
        // normalized subpath includes the parent folder is the better match.
        var betterMatch = assets.FirstOrDefault(k => Equals(k.SubPathNormalized, lastChance));
        if (betterMatch is not null)
            return betterMatch;

        return assets[0];
    }

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} Key:{1}", nameof(StaticWebAssetEntry), Key);

    private class EntryContentWrapper
    {
        public StaticWebAssetEntryContent? Asset { get; set; }
    }
}

internal record StaticWebAssetEntryContent
{
    public string? SubPath { get; init; }

    /// <summary>
    /// Index into <see cref="StaticWebAssetContent.ContentRoots"/>.
    /// </summary>
    public int ContentRootIndex { get; init; }

    public string? SubPathNormalized
    {
        get
        {
            if (string.IsNullOrEmpty(SubPath))
                return null;

            return PathHelper.NormalizePath(SubPath);
        }
    }
}
