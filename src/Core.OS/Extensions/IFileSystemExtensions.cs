using System.IO.Abstractions;

namespace Core.OS.Extensions;

internal static partial class IFileSystemExtensions
{
    extension(IFileSystem fileSystem)
    {
        public void TryDeleteDirectories(IEnumerable<string> directories, string source, ILogger? logger = null)
        {
            foreach (var directory in directories)
            {
                fileSystem.TryDeleteDirectory(directory, source, logger);
            }
        }

        public void TryDeleteDirectory(string directory, string source, ILogger? logger = null)
        {
            var directoryName = directory.EndsWith(fileSystem.Path.DirectorySeparatorChar)
                ? fileSystem.Path.GetDirectoryName(directory)
                : fileSystem.Path.GetFileName(directory);

            try
            {
                if (!fileSystem.Directory.Exists(directory))
                    return;

                fileSystem.Directory.Delete(directory, true);
                if (logger is not null) LogResetWorkspaceDone(logger, source, directoryName);
            }
            catch (UnauthorizedAccessException ue)
            {
                if (logger is not null) LogDeleteWorkspaceDirectoryUnauthorized(logger, ue, directoryName);
            }
            catch (Exception e)
            {
                if (logger is not null) LogDeleteWorkspaceDirectoryFailed(logger, e, source, directoryName);
            }
        }

        public void TryDeleteFiles(IEnumerable<string> files, string source, ILogger? logger = null)
        {
            foreach (var file in files)
            {
                var fileName = fileSystem.Path.GetFileName(file);

                try
                {
                    if (!fileSystem.File.Exists(file))
                        continue;

                    fileSystem.File.Delete(file);
                    if (logger is not null) LogDeleteWorkspaceFile(logger, source, fileName);
                }
                catch (UnauthorizedAccessException ue)
                {
                    if (logger is not null) LogDeleteWorkspaceFileUnauthorized(logger, ue, source);
                }
                catch (Exception e)
                {
                    if (logger is not null) LogDeleteWorkspaceFileFailed(logger, e, source, fileName);
                }
            }
        }
    }
}
