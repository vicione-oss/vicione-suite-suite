using System.IO.Abstractions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;

namespace Core.OS.Hosting.Extensions;

internal static class IFileSystemExtensions
{
    extension(IFileSystem fileSystem)
    {
        internal string GetLocalDataVersionFilePath(InstanceOptions instanceOptions)
            => fileSystem.Path.Combine(fileSystem.GetRootedHomeDirectory(instanceOptions), Constants.DataVersionFileName);

        public bool DataVersionFileExists(InstanceOptions options)
            => fileSystem.File.Exists(fileSystem.GetLocalDataVersionFilePath(options));

        public Task WriteDataVersionFile(InstanceOptions options, string version, CancellationToken cancellationToken = default)
        {
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);

            // good for short strings
            return fileSystem.File.WriteAllTextAsync(dataVersionPath, version, cancellationToken);
        }

        public Task<string> ReadDataVersionFile(InstanceOptions options, CancellationToken cancellationToken = default)
        {
            var dataVersionPath = fileSystem.GetLocalDataVersionFilePath(options);

            // good for short strings
            return fileSystem.File.ReadAllTextAsync(dataVersionPath, cancellationToken);
        }
    }
}
