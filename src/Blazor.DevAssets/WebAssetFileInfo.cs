using Microsoft.Extensions.FileProviders;

namespace Blazor.DevAssets;

internal sealed class WebAssetFileInfo(System.IO.Abstractions.IFileInfo info) : IFileInfo
{
    public bool Exists => info.Exists;
    public bool IsDirectory => false;
    public DateTimeOffset LastModified => info.LastWriteTime;
    public long Length => info.Length;
    public string Name => info.Name;
    public string PhysicalPath => info.FullName;

    public Stream CreateReadStream() => info.OpenRead();
}
