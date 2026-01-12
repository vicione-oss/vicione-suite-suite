using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;

namespace Core.OS.Hosting.Extensions;

internal static class IFileSystemExtensions
{
    internal static string GetLocalDataVersionFilePath(this IFileSystem fileSystem, InstanceOptions instanceOptions)
        => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), Constants.DataVersionFileName);

    public static bool DataVersionFileExists(this IFileSystem fileSystem, InstanceOptions options)
        => fileSystem.File.Exists(fileSystem.GetLocalDataVersionFilePath(options));

    public static Task WriteDataVersionFile(this IFileSystem fileSystem, InstanceOptions options, string version, CancellationToken cancellationToken = default)
    {
        var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);

        // good for short strings
        return fileSystem.File.WriteAllTextAsync(dataVersionPath, version, cancellationToken);
    }

    public static Task<string> ReadDataVersionFile(this IFileSystem fileSystem, InstanceOptions options, CancellationToken cancellationToken = default)
    {
        var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);

        // good for short strings
        return fileSystem.File.ReadAllTextAsync(dataVersionPath, cancellationToken);
    }
}
