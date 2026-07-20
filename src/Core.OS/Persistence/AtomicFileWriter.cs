using System.IO.Abstractions;
using Core.OS.Modules.Extensions;
using Sdk.Backend.IO;

namespace Core.OS.Persistence;

/// <summary>
/// Default <see cref="IAtomicFileWriter"/> implementation backed by an <see cref="IFileSystem"/>.
/// </summary>
internal sealed class AtomicFileWriter(IFileSystem fileSystem) : IAtomicFileWriter
{
    /// <inheritdoc />
    public Task WriteAsync(string filePath, Func<Stream, Task> writeContent, CancellationToken cancellationToken = default)
        => fileSystem.WriteFileAtomic(filePath, writeContent, cancellationToken);
}
