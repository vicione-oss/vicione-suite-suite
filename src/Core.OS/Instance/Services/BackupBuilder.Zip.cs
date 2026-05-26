using System.IO.Abstractions;
using System.IO.Compression;
using System.IO.Enumeration;
using Core.Shared.Persistence.Contracts;

namespace Core.OS.Instance.Services;

internal partial class BackupBuilder
{
    private enum CreateEntryType
    {
        File,
        Directory,
        Unsupported
    }

    private static FileSystemEnumerable<(string, CreateEntryType)> CreateEnumerableForCreate(string directoryFullPath)
        => new(directoryFullPath,
            static (ref entry) => (entry.ToFullPath(), entry.IsDirectory ? CreateEntryType.Directory : CreateEntryType.File),
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = 0,
                IgnoreInaccessible = false
            });

    /// <summary>
    /// We can't use <see cref="ZipFile.CreateFromDirectory(string,System.IO.Stream)"/> because it attempts to copy the open sqlite databases.
    /// Fails with IO.Exceptions and therefore we need to create this archive on our own and give the db files special treatment
    /// </summary>
    /// <param name="fileSystem"></param>
    /// <param name="moduleArchive"></param>
    /// <param name="sourceDirectory"></param>
    /// <param name="summary"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="IOException"></exception>
    private static async Task AddModuleDirectoryToArchive(IFileSystem fileSystem, ZipArchive moduleArchive, string sourceDirectory, BackupModuleSummary summary, CancellationToken cancellationToken)
    {
        string[] ignoreExtensions = [".db-wal", ".db-shm"];
        var directoryIsEmpty = true;
        var databaseFiles = new Dictionary<string, string>();

        //add files and directories
        var di = new DirectoryInfo(sourceDirectory);

        var basePath = di.FullName;

        if (di.Parent != null)
            basePath = di.Parent.FullName;

        var fse = CreateEnumerableForCreate(di.FullName);

        foreach (var (fullPath, type) in fse)
        {
            directoryIsEmpty = false;

            switch (type)
            {
                case CreateEntryType.File:
                    {
                        var extension = fileSystem.Path.GetExtension(fullPath);

                        // ignore things
                        if (ignoreExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                            continue;

                        // create entry for file
                        var entryName = CreateEntryName(fileSystem, basePath, fullPath);

                        if (extension.Equals(SqliteDbExtensions, StringComparison.OrdinalIgnoreCase))
                        {
                            databaseFiles.TryAdd(entryName, fullPath);
                            continue;
                        }

                        await moduleArchive.CreateEntryFromFileAsync(fullPath, entryName, CompressionLevel.Optimal, cancellationToken);
                    }
                    break;
                case CreateEntryType.Directory:
                    if (IsDirEmpty(fullPath))
                    {
                        // Create entry marking an empty dir:
                        // FullName never returns a directory separator character on the end,
                        // but Zip archives require it to specify an explicit directory:
                        var entryName = CreateEntryName(fileSystem, basePath, fullPath) + fileSystem.Path.DirectorySeparatorChar;
                        moduleArchive.CreateEntry(entryName);
                    }
                    break;
                case CreateEntryType.Unsupported:
                default:
                    throw new IOException($"Failed to create zip entry for {fullPath}");
            }
        }

        // backup database files
        summary.Databases = await BackupSqliteDatabases(moduleArchive, fileSystem, sourceDirectory, databaseFiles, cancellationToken);
        //summary.ArchiveLength = entryStream.Position;

        // If no entries create an empty root directory entry:
        if (directoryIsEmpty)
        {
            var entryName = CreateEntryName(fileSystem, basePath, di.FullName) + fileSystem.Path.DirectorySeparatorChar;
            moduleArchive.CreateEntry(entryName);
        }
    }

    private static string CreateEntryName(IFileSystem fileSystem, string sourceDirectory, string filePath)
        => fileSystem.Path.GetRelativePath(sourceDirectory, filePath)
            .Replace("\\", "/");// for cross-platform compatibility

    private static bool IsDirEmpty(string directoryFullName)
    {
        using var enumerator = Directory.EnumerateFileSystemEntries(directoryFullName).GetEnumerator();
        return !enumerator.MoveNext();
    }
}
