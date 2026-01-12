using System.Diagnostics;
using System.Text.Json;
using MassTransit.Metadata;

namespace Blazor.DevAssets;

/// <summary>
/// Parse staticwebassets.runtime.json to be able to resolve requests from the debug webserver
/// to absolute file paths. Was not able to find a default reader for it.
/// </summary>
internal sealed class StaticWebAssetContent(string webassetJsonPath)
{
    public const string ChildrenPropertyName = "Children";
    public const string AssetPropertyName = "Asset";
    public const string PatternsPropertyName = "Patterns";

    private readonly List<StaticWebAssetEntry> _entries = [];
    private readonly List<string> _contentRoots = [];
    private readonly string _jsonPath = webassetJsonPath;

    public IReadOnlyList<StaticWebAssetEntry> Entries => _entries;
    public IReadOnlyList<string> ContentRoots => _contentRoots;

    public string? FindAssetContentPath(string subpath, string fallbackFileName, string lastChance)
    {
        // try to find a matching asset inside our entries
        foreach (var entry in _entries)
        {
            var content = entry.FindAssetContent(subpath, fallbackFileName, lastChance);
            if (content is null || string.IsNullOrEmpty(content.SubPath))
                continue;

            if (content.ContentRootIndex >= ContentRoots.Count)
                continue;

            return Path.Combine(ContentRoots[content.ContentRootIndex], content.SubPath);
        }

        return null;
    }

    /// <summary>
    /// maps the content root paths from the staticwebassets.json to corresponding paths 
    /// on the docker container. Mapping only in DEBUG
    /// </summary>
    /// <param name="contentRoots"></param>
    /// <returns></returns>
    public static IEnumerable<string> FixDockerDebugContentRootPaths(IEnumerable<string>? contentRoots)
    {

#if !DEBUG
            return contentRoots ?? new string[0];
#else
        if (contentRoots is null)
            return [];

        if (!HostMetadataCache.IsRunningInContainer)
            return contentRoots;

        var result = new List<string>();

        foreach (var contentRoot in contentRoots)
        {
            if (!WebAssetContentRegex.IsMatch(contentRoot))
            {
                continue;
            }

            // Group[0] D:\\path\\to\\repo\\[src|.nuget]\\relative\\project\\path
            // Group[1] D:
            // Group[2] \\path\\to\\repo\\
            // Group[3] .nuget|src
            // Group[4] \\relative\\project\\path
            var groups = WebAssetContentRegex.Matches(contentRoot)[0].Groups;
            var projectParts = groups[4].Value.Split('\\');
            string projectPath;
            string sourcePath;

            if (groups[3].Value == ".nuget")
            {
                if (string.Equals(projectParts[0], "packages", StringComparison.OrdinalIgnoreCase))
                {
                    projectParts = projectParts[1..];
                }

                // nuget package reference -> /root/.nuget/fallbackpages/path/to/project
                sourcePath = Path.Combine(["/root", ".nuget", "fallbackpackages"]);
                projectPath = Path.Combine(projectParts);
            }
            else
            {
                // project reference -> /src/src/path/to/project/output
                sourcePath = Path.Combine(["/src", "src"]);
                projectPath = Path.Combine(projectParts);
            }

            result.Add($"{Path.Combine(sourcePath, projectPath)}{Path.DirectorySeparatorChar}");
        }

        return result;
#endif
    }

    public void ParseWebAssetContents()
    {
        if (!File.Exists(_jsonPath))
            throw new FileNotFoundException(_jsonPath);

        var json = File.ReadAllText(_jsonPath);
        var doc = JsonDocument.Parse(json);

        foreach (var child in doc.RootElement.EnumerateObject())
        {
            // array of paths that used by asset.rootindex to build an absolute path 
            if (child.Name == "ContentRoots")
            {
                var array = child.Value.Deserialize<string[]>();

                _contentRoots.AddRange(FixDockerDebugContentRootPaths(array));
                continue;
            }

            // recursive contents like js, svg, css, _content...
            if (child.Name == "Root")
            {
                AddRootFolderContentAssets(child);
            }
        }
    }

    private void AddRootFolderContentAssets(JsonProperty root)
    {
        // Root -> Children, Asset, Patterns
        foreach (var child in root.Value.EnumerateObject())
        {
            // skip Asset, Pattern
            if (child.Name != ChildrenPropertyName || child.Value.ValueKind != JsonValueKind.Object)
                continue;

            // folders like svg, js, ..
            foreach (var folder in child.Value.EnumerateObject())
            {
                AddFolderContentEntries(folder);
            }
        }
    }

    private void AddFolderContentEntries(JsonProperty folder)
    {
        AddRootFolderContentEntries(folder.Value, folder.Name);
    }

    private void AddRootFolderContentEntries(JsonElement? folder, string? folderName = null)
    {
        if (folder is null || folder.Value.ValueKind != JsonValueKind.Object)
            return;

        // e.g. folder -> Root.Children.js
        foreach (var child in folder.Value.EnumerateObject())
        {
            if (child.Value.ValueKind != JsonValueKind.Object)
                continue;

            // e.g. Root.Children.index.html.Asset
            if (child.Name == AssetPropertyName)
            {
                _entries.Add(new StaticWebAssetEntry(folder) { Key = folderName });
                continue;
            }

            if (child.Name == ChildrenPropertyName)
            {
                // Root.Children.js.Children
                foreach (var entry in child.Value.EnumerateObject())
                {
                    // this is a special case dotnet.6.0.0-rc.1.21451.13.js -> dotnet.js
                    if (WebAssetDotnetRegex.IsMatch(entry.Name))
                    {
                        var key = WebAssetDotnetRegex.Replace(entry.Name);

                        _entries.Add(new StaticWebAssetEntry(entry.Value) { Key = key });
                        // afterwards it will be added a second time with the version info
                    }

                    // "DevExpress.Blazor.Dashboard": { .. }
                    _entries.Add(new StaticWebAssetEntry(entry.Value) { Key = entry.Name });
                }
            }
        }
    }

    public void DebugDump()
    {
        foreach (var item in Entries)
        {
            Debug.WriteLine("Entry.Key: {0} -> asset.count={1}", item.Key, item.Assets.Count);

            foreach (var asset in item.Assets)
            {
                Debug.WriteLine("subpath={0} normalized={1}", asset.SubPath, asset.SubPathNormalized);
            }
        }
    }
}
