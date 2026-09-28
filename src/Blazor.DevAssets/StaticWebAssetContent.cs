using System.IO.Abstractions;
using System.Text.Json;
using MassTransit.Metadata;

namespace Blazor.DevAssets;

/// <summary>
/// Parses staticwebassets.runtime.json to resolve debug web server requests to absolute file paths.
/// Hand-rolled because the framework exposes no reader for the format.
/// </summary>
internal sealed class StaticWebAssetContent(IFileSystem fileSystem, string webassetJsonPath)
{
    private const string ManifestSuffix = ".staticwebassets.runtime.json";

    /// <summary>
    /// Matches route segments the way the framework's own development file provider does.
    /// </summary>
    private static readonly StringComparer RouteComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly List<string> _contentRoots = [];
    private readonly string _jsonPath = webassetJsonPath;
    private Node? _root;

    public IReadOnlyList<string> ContentRoots => _contentRoots;

    /// <summary>
    /// The project the manifest describes, e.g. ViciOne.Suite.Blazor.Shared for ViciOne.Suite.Blazor.Shared.staticwebassets.runtime.json.
    /// </summary>
    public string Name { get; } = GetName(fileSystem, webassetJsonPath);

    /// <summary>
    /// Returns the file served at <paramref name="route"/>, or <see langword="null"/> when the manifest has no such route.
    /// </summary>
    /// <remarks>
    /// A route can differ from the file it serves: the fingerprint in a scoped CSS bundle URL of a project reference only
    /// reaches the file name on publish.
    /// </remarks>
    public string? FindAssetPath(ReadOnlySpan<string> route)
    {
        var node = _root;
        foreach (var segment in route)
        {
            if (node?.Children is null || !node.Children.TryGetValue(segment, out node))
                return null;
        }

        if (node?.Asset is not { } asset || asset.ContentRootIndex >= _contentRoots.Count)
            return null;

        return fileSystem.Path.Combine(_contentRoots[asset.ContentRootIndex], asset.SubPath);
    }

    /// <summary>
    /// Maps content root paths from staticwebassets.json onto their docker container paths. DEBUG only.
    /// </summary>
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

            // Group[0] D:\path\to\repo\[src|.nuget]\relative\project\path
            // Group[1] D:
            // Group[2] \path\to\repo\
            // Group[3] .nuget|src
            // Group[4] \relative\project\path
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

                // Nuget package reference -> /root/.nuget/fallbackpackages/path/to/project.
                sourcePath = Path.Combine(["/root", ".nuget", "fallbackpackages"]);
                projectPath = Path.Combine(projectParts);
            }
            else
            {
                // Project reference -> /src/src/path/to/project/output.
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
        if (!fileSystem.File.Exists(_jsonPath))
            throw new FileNotFoundException(_jsonPath);

        using var doc = JsonDocument.Parse(fileSystem.File.ReadAllText(_jsonPath));

        // Indexed by asset.rootindex to build an absolute path.
        if (doc.RootElement.TryGetProperty("ContentRoots", out var contentRoots))
            _contentRoots.AddRange(FixDockerDebugContentRootPaths(contentRoots.EnumerateArray().Select(contentRoot => contentRoot.GetString()!)));

        // Each segment of a route names a child node; Patterns, which match files added after the build, are skipped.
        if (doc.RootElement.TryGetProperty("Root", out var root))
            _root = ParseNode(root);
    }

    private static Node ParseNode(JsonElement element)
    {
        Dictionary<string, Node>? children = null;
        if (element.TryGetProperty("Children", out var childElements) && childElements.ValueKind == JsonValueKind.Object)
        {
            children = new Dictionary<string, Node>(RouteComparer);
            foreach (var child in childElements.EnumerateObject())
                children[child.Name] = ParseNode(child.Value);
        }

        Asset? asset = null;
        if (element.TryGetProperty("Asset", out var match) && match.ValueKind == JsonValueKind.Object
            && match.GetProperty("SubPath").GetString() is { Length: > 0 } subPath)
            asset = new Asset(match.GetProperty("ContentRootIndex").GetInt32(), subPath);

        return new Node(children, asset);
    }

    private static string GetName(IFileSystem fileSystem, string jsonPath)
    {
        var fileName = fileSystem.Path.GetFileName(jsonPath);
        return fileName.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase) ? fileName[..^ManifestSuffix.Length] : fileName;
    }

    private sealed record Node(Dictionary<string, Node>? Children, Asset? Asset);

    /// <param name="ContentRootIndex">Index into <see cref="ContentRoots"/>.</param>
    /// <param name="SubPath">Path of the file relative to that content root.</param>
    private sealed record Asset(int ContentRootIndex, string SubPath);
}
