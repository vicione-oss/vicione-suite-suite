using System.IO.Abstractions;

namespace Core.OS.Extensions;

internal static class IFileSystemExtensions
{
    extension(IFileSystem fileSystem)
    {
        public void TryDeleteDirectories(IEnumerable<string> directories, string source, Serilog.ILogger? logger = null)
        {
            foreach (var directory in directories)
            {
                fileSystem.TryDeleteDirectory(directory, source, logger);
            }
        }

        public void TryDeleteDirectory(string directory, string source, Serilog.ILogger? logger = null)
        {
            var directoryName = directory.EndsWith(fileSystem.Path.DirectorySeparatorChar)
                ? fileSystem.Path.GetDirectoryName(directory)
                : fileSystem.Path.GetFileName(directory);

            try
            {
                if (!fileSystem.Directory.Exists(directory))
                    return;

                fileSystem.Directory.Delete(directory, true);
                logger?.Information("Reset {Source} workspace '{Name}' done", source, directoryName);
            }
            catch (UnauthorizedAccessException ue)
            {
                logger?.Error(ue, "Insufficient permissions to delete {Source} workspace directory", directoryName);
            }
            catch (Exception e)
            {
                logger?.Error(e, "Failed to reset {Source} workspace '{Name}'.", source, directoryName);
            }
        }

        public void TryDeleteFiles(IEnumerable<string> files, string source, Serilog.ILogger? logger = null)
        {
            foreach (var file in files)
            {
                var fileName = fileSystem.Path.GetFileName(file);

                try
                {
                    if (!fileSystem.File.Exists(file))
                        continue;

                    fileSystem.File.Delete(file);
                    logger?.Information("Delete {Source} workspace file '{Name}'.", source, fileName);
                }
                catch (UnauthorizedAccessException ue)
                {
                    logger?.Error(ue, "Insufficient permissions to delete {Source} workspace file", source);
                }
                catch (Exception e)
                {
                    logger?.Error(e, "Failed to delete {Source} workspace file '{Name}'.", source, fileName);
                }
            }
        }
    }
}
