using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Blazor.DevAssets;

/// <summary>
/// Resolves asset requests against the *staticwebassets.runtime.json files of the debug build output.
/// Known gap: Backend.Login's /Identity/js/site.js is not published.
/// </summary>
internal sealed class DevelopmentFileProvider : IFileProvider
{
    private const string StaticWebAssetsExtensions = "*staticwebassets.runtime.json";

    private readonly List<StaticWebAssetContent> _webAssetFileContents = [];
    private readonly HashSet<string> _loadedAssets = [];

    public void AddStaticWebAssetJsonsFromPath(string assetPath)
    {
        if (!_loadedAssets.Add(assetPath))
            return;

        var jsonAssets = Directory.GetFiles(assetPath, StaticWebAssetsExtensions, SearchOption.TopDirectoryOnly);

        foreach (var jsonPath in jsonAssets)
        {
            AddStaticWebAssetJson(jsonPath);
        }
    }

    public void AddStaticWebAssetJsons(IEnumerable<string> jsonPaths)
    {
        foreach (var jsonPath in jsonPaths)
        {
            AddStaticWebAssetJson(jsonPath);
        }
    }

    /// <summary>
    /// Parses a staticwebassets.runtime.json and registers its entries for asset resolution.
    /// </summary>
    public void AddStaticWebAssetJson(string jsonPath)
    {
        var content = new StaticWebAssetContent(jsonPath);

        content.ParseWebAssetContents();

        _webAssetFileContents.Add(content);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        // Not observed in practice; asset resolution goes through GetFileInfo.
        return NotFoundDirectoryContents.Singleton;
    }

    /// <summary>
    /// Resolves an incoming asset request to an absolute file path.
    /// </summary>
    public IFileInfo GetFileInfo(string subpath)
    {
        if (subpath.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
        {
            return new NotFoundFileInfo(subpath); // Brotli variants are skipped to speed up debug.
        }

        var contentFilePath = FindAssetNormalizedPath(subpath);
        if (contentFilePath is null)
        {
            Debug.WriteLine("{0} failed to resolve {1}", nameof(GetFileInfo), subpath);
            return new NotFoundFileInfo(subpath);
        }

        return !File.Exists(contentFilePath) ? new NotFoundFileInfo(contentFilePath) : new WebAssetFileInfo(contentFilePath);
    }

    private string? FindAssetNormalizedPath(string subpath)
    {
        var search = PathHelper.NormalizePath(TrimStartDirectoryChar(subpath));
        var fileName = Path.GetFileName(subpath);
        var lastChance = GetRelativePathWithParent(subpath) ?? string.Empty;

        // NuGet package assets resolve differently, e.g. _content/ViciOne.Ui.ClusterEditor/assets/icon.png.
        var match = WebAssetContentNuGetRegex.Match(search);
        if (match.Success)
        {
            var trimmedSearch = search[match.Value.Length..];

            // The full package name targets the exact staticwebassets.runtime.json.
            var packageName = ExtractPackageNameFromContentPath(subpath);
            if (packageName is not null)
            {
                var packageContent = FindPackageContent(packageName);
                var directPath = packageContent?.FindAssetContentPath(trimmedSearch, fileName, lastChance);
                if (directPath is not null)
                    return directPath;
            }

            search = trimmedSearch;
        }

        var possibleAssetPaths = _webAssetFileContents
            .Select(c => c.FindAssetContentPath(search, fileName, lastChance))
            .Where(p => p is not null)
            .Distinct()
            .ToList();

        return possibleAssetPaths.FirstOrDefault();
    }

    /// <summary>
    /// Finds the content parsed from {packageName}.staticwebassets.runtime.json.
    /// </summary>
    private StaticWebAssetContent? FindPackageContent(string packageName)
        => _webAssetFileContents.FirstOrDefault(c =>
            Path.GetFileName(c.JsonPath).StartsWith(packageName + ".", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Extracts the package name from a _content/ subpath:
    /// /_content/ViciOne.Suite.ClusterManagement.Client/icons/bundle.css -> ViciOne.Suite.ClusterManagement.Client
    /// </summary>
    internal static string? ExtractPackageNameFromContentPath(string subpath)
    {
        const string contentPrefix = "_content/";
        var contentIndex = subpath.IndexOf(contentPrefix, StringComparison.OrdinalIgnoreCase);
        if (contentIndex < 0)
            return null;

        var afterContent = subpath[(contentIndex + contentPrefix.Length)..];
        var separatorIndex = afterContent.IndexOfAny(['/', '\\']);
        return separatorIndex >= 0 ? afterContent[..separatorIndex] : afterContent;
    }

    private static string TrimStartDirectoryChar(string path)
    {
        if (path.StartsWith(Path.DirectorySeparatorChar) || path.StartsWith(Path.AltDirectorySeparatorChar))
            return path[1..];

        return path;
    }

    /// <summary>
    /// Returns parent\filename.ext, e.g. /_content/ViciOne.Suite.Ping.Client/svg/toolbox.svg yields svg\toolbox.svg.
    /// </summary>
    private static string? GetRelativePathWithParent(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            var normalized = PathHelper.NormalizePath(path);
            var parent = Directory.GetParent(normalized);

            return Path.Combine(parent!.Name, Path.GetFileName(normalized));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public IChangeToken Watch(string filter) => new ChangeToken();

    private class ChangeToken : IChangeToken
    {
        public bool ActiveChangeCallbacks => false;

        public bool HasChanged => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new Disposable();
    }

    private sealed class Disposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
