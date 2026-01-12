using Microsoft.Extensions.FileProviders;

namespace Blazor.DevAssets;

internal sealed class WebAssetFileInfo(FileInfo info) : IFileInfo
{
    private readonly FileInfo _info = info;

    public bool Exists => _info.Exists;
    public bool IsDirectory => false;
    public DateTimeOffset LastModified => _info.LastWriteTime;
    public long Length => _info.Length;
    public string Name => _info.Name;
    public string PhysicalPath => _info.FullName;

    public WebAssetFileInfo(string filePath)
        : this(new FileInfo(filePath))
    {
    }

    public Stream CreateReadStream() => File.OpenRead(_info.FullName);
}
