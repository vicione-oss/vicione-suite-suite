using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Blazor.DevAssets;

internal sealed class ModuleFileProvider : IFileProvider, IDisposable
{
    private readonly string _moduleMatch;
    private readonly PhysicalFileProvider _fileProvider;

    public ModuleFileProvider(string moduleDllName, string rootPath, string contentPrefix)
    {
        var module = moduleDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ?
            Path.GetFileNameWithoutExtension(moduleDllName) : moduleDllName;

        _moduleMatch = $"/{contentPrefix}/{module}";
        _fileProvider = new PhysicalFileProvider(rootPath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        if (!subpath.StartsWith(_moduleMatch, StringComparison.Ordinal))
            return NotFoundDirectoryContents.Singleton;

        return _fileProvider.GetDirectoryContents(subpath);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        // handle resource calls like /_content/Module.Dll.Name/svg/thing.svg
        if (subpath.StartsWith(_moduleMatch, StringComparison.Ordinal))
            return _fileProvider.GetFileInfo(subpath.Remove(0, _moduleMatch.Length));

        // handle request to additional resources published in rootPath/_content where
        // referenced nuget resources get published
        return _fileProvider.GetFileInfo(subpath);
    }

    public IChangeToken Watch(string filter) => new ChangeToken();

    public void Dispose() => _fileProvider.Dispose();

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
