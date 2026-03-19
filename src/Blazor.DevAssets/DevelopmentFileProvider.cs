using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Blazor.DevAssets;

/// <summary>
/// One issue with Backend.Login -> /Identity/js/site.js does not get published?!
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
    /// parse the staticwebasset.json and add it so it can be used to resolve asset requests
    /// </summary>
    /// <param name="jsonPath"></param>
    public void AddStaticWebAssetJson(string jsonPath)
    {
        var content = new StaticWebAssetContent(jsonPath);

        content.ParseWebAssetContents();

        _webAssetFileContents.Add(content);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        // never noticed it gets called so far
        return NotFoundDirectoryContents.Singleton;
    }

    /// <summary>
    /// resolves incoming asset requests to absolute file paths       
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IFileInfo GetFileInfo(string subpath)
    {
        if (subpath.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
        {
            return new NotFoundFileInfo(subpath); // skip the brotli requests to speed up debug
        }

        // using the infos from parsed webasset json we try to find asset for the request to get an absolute path
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
        // we need to remove the trailing slash and normalize the path for comparison
        var search = PathHelper.NormalizePath(TrimStartDirectoryChar(subpath));
        var fileName = Path.GetFileName(subpath);
        var lastChance = GetRelativePathWithParent(subpath) ?? string.Empty;

        // for assets of referenced nuget packages we need to resolve differently
        // _content/ViciOne.Ui.ClusterEditor/assets/context_menu/SelectAll_16x16.png
        var match = WebAssetContentNuGetRegex.Match(search);
        if (match.Success)
        {
            var trimmedSearch = search[match.Value.Length..];

            // use the full package name from _content/{packageName}/ to target the exact
            // staticwebassets.runtime.json — e.g. ViciOne.Suite.ClusterManagement.Client
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

        // try to find an entry that contains the requested subpath
        var possibleAssetPaths = _webAssetFileContents
            .Select(c => c.FindAssetContentPath(search, fileName, lastChance))
            .Where(p => p is not null)
            .Distinct()
            .ToList();

        return possibleAssetPaths.FirstOrDefault();
    }

    /// <summary>
    /// finds the StaticWebAssetContent whose JSON filename matches
    /// {packageName}.staticwebassets.runtime.json
    /// </summary>
    private StaticWebAssetContent? FindPackageContent(string packageName)
        => _webAssetFileContents.FirstOrDefault(c =>
            Path.GetFileName(c.JsonPath).StartsWith(packageName + ".", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// extracts the package name from a _content/ subpath
    /// e.g. /_content/ViciOne.Suite.ClusterManagement.Client/icons/bundle.css
    ///   -> ViciOne.Suite.ClusterManagement.Client
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
    /// return parent\\filename.ext from a path
    /// </summary>
    /// <param name="path">e.g. /_content/ViciOne.Suite.Ping.Client/svg/toolbox.svg</param>
    /// <returns>svg\\toolbox.svg</returns>
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
