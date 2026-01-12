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
            // e.g. Root.Children.js.dashboard-blazor.js.Children
            if (item.Name == StaticWebAssetContent.ChildrenPropertyName)
            {
                AddChildrenR(item);
            }

            // e.g. Root.Children.js.dashboard-blazor.js.Asset
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
                // try to get to the leaves
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
        // 1. try to get subpath match first (best chance!)
        // 2. module scoped css is different -> try the filename
        // 3. the last chance to try to get stuff like svg/some.svg 
        var assets = Assets
            .Where(k => !string.IsNullOrEmpty(k.SubPath) &&
                        (Equals(k.SubPathNormalized, subpath) || Equals(k.SubPath, fallbackFileName) || Equals(k.SubPathNormalized, lastChance)))
            .ToArray();

        if (assets.Length == 0)
            return null;

        // the default case
        if (assets.Length == 1)
            return assets[0];

        // now we have multiple assets for a subpath is like /_content/DevExpress.Blazor/modules/dx-blazor-750968d4.js
        // assets[0] -> dx-blazor-750968d4.js
        // assets[1] -> modules/dx-blazor-750968d4.js   -> this is the one with better match            
        var betterMatch = assets.FirstOrDefault(k => Equals(k.SubPathNormalized, lastChance));
        if (betterMatch is not null)
            return betterMatch;

        return assets[0];
    }

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} Key:{1}", nameof(StaticWebAssetEntry), Key);

    /// <summary>
    /// only a wrapper for deserialize
    /// </summary>
    private class EntryContentWrapper
    {
        public StaticWebAssetEntryContent? Asset { get; set; }
    }
}

internal record StaticWebAssetEntryContent
{
    public string? SubPath { get; init; }

    /// <summary>
    /// reference to the referenced content folder
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
