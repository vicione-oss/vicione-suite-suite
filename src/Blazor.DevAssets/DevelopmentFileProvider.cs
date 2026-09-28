using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Sdk.Client.Modules;
using IFileSystem = System.IO.Abstractions.IFileSystem;

namespace Blazor.DevAssets;

/// <summary>
/// Resolves asset requests against the *staticwebassets.runtime.json files of the debug build output.
/// Known gap: Backend.Login's /Identity/js/site.js is not published.
/// </summary>
internal sealed class DevelopmentFileProvider(IFileSystem fileSystem) : IFileProvider
{
    private const string StaticWebAssetsExtensions = "*staticwebassets.runtime.json";

    private readonly List<StaticWebAssetContent> _webAssetFileContents = [];
    private readonly HashSet<string> _loadedAssets = [];

    public void AddStaticWebAssetJsonsFromPath(string assetPath)
    {
        if (!_loadedAssets.Add(assetPath))
            return;

        var jsonAssets = fileSystem.Directory.GetFiles(assetPath, StaticWebAssetsExtensions, SearchOption.TopDirectoryOnly);

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
        var content = new StaticWebAssetContent(fileSystem, jsonPath);

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

        var contentFilePath = FindAssetPath(subpath);
        if (contentFilePath is null)
        {
            Debug.WriteLine("{0} failed to resolve {1}", nameof(GetFileInfo), subpath);
            return new NotFoundFileInfo(subpath);
        }

        return !fileSystem.File.Exists(contentFilePath)
            ? new NotFoundFileInfo(contentFilePath)
            : new WebAssetFileInfo(fileSystem.FileInfo.New(contentFilePath));
    }

    /// <summary>
    /// Looks the request up as a route of the loaded manifests, first in the manifest of the project a
    /// /_content/{Name}/ request addresses, then in all of them.
    /// </summary>
    /// <remarks>
    /// The project manifest comes first because a published module serves its own assets, and the imports of its
    /// stylesheet bundle, e.g. /_content/{Module}/_content/{Library}/..., from its wwwroot below that prefix.
    /// </remarks>
    private string? FindAssetPath(string subpath)
    {
        var route = subpath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (route.Length > 2 && route[0] == ModuleAssetHelper.ContentPrefix)
        {
            var projectRoute = route.AsSpan(2);
            foreach (var content in _webAssetFileContents)
            {
                if (string.Equals(content.Name, route[1], StringComparison.OrdinalIgnoreCase)
                    && content.FindAssetPath(projectRoute) is { } projectPath)
                    return projectPath;
            }
        }

        foreach (var content in _webAssetFileContents)
        {
            if (content.FindAssetPath(route) is { } path)
                return path;
        }

        return null;
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
