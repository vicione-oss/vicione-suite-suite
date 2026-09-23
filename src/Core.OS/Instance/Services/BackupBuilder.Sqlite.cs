using System.IO.Abstractions;
using System.IO.Compression;
using Core.Shared.Persistence.Contracts;
using Microsoft.Data.Sqlite;

namespace Core.OS.Instance.Services;

internal partial class BackupBuilder
{
    private static async Task<List<BackupEntrySummary>?> BackupSqliteDatabases(ZipArchive moduleArchive, IFileSystem fileSystem, Dictionary<string, string> databaseFiles, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (databaseFiles.Count == 0)
            return null;

        var result = new List<BackupEntrySummary>();

        // We can't backup the sqlite database directly to a stream.
        // We can't just copy the db files, because it might corrupt the data
        // Therefore we need to backup databases to new temporary files and add them to the archive instead.
        var tmpDirectory = fileSystem.Directory.CreateTempSubdirectory();

        try
        {
            foreach (var databaseFile in databaseFiles)
            {
                var dbSummary = new BackupEntrySummary
                {
                    Name = databaseFile.Key,
                    EntryName = databaseFile.Key,
                };

                try
                {
                    LogCreatingDatabaseEntry(logger, databaseFile.Key);

                    var newEntry = moduleArchive.CreateEntry(databaseFile.Key);
                    await using var destStream = await newEntry.OpenAsync(cancellationToken);
                    var sourceDbPath = databaseFile.Value; // contains the full path to the database file

                    await BackupSqliteDatabase(fileSystem, sourceDbPath, tmpDirectory, destStream, cancellationToken);
                    dbSummary.ArchiveLength = destStream.Position;

                    LogCreatedDatabaseEntry(logger, databaseFile.Key, destStream.Position);
                }
                catch (OperationCanceledException)
                {
                    throw;  // exit loop on cancellation without logging an error
                }
                catch (Exception e)
                {
                    LogFailedToCreateDatabaseEntry(logger, e, databaseFile.Key);

                    dbSummary.Error = e.Message;
                }

                result.Add(dbSummary);
            }
        }
        catch (Exception ex)
        {
            LogUnexpectedBackupSqliteError(logger, ex);
        }
        finally
        {
            fileSystem.Directory.Delete(tmpDirectory.FullName, true);
        }

        return result;
    }

    private static async Task BackupSqliteDatabase(IFileSystem fileSystem, string sourceDbPath, IDirectoryInfo tmpDirectory, Stream destinationStream, CancellationToken cancellationToken = default)
    {
        if (!fileSystem.File.Exists(sourceDbPath))
            throw new FileNotFoundException($"The file '{sourceDbPath}' was not found.");

        var sourceOptions = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDbPath,
            Mode = SqliteOpenMode.ReadOnly,
        };

        var dbName = fileSystem.Path.GetFileName(sourceDbPath);
        var destinationDbPath = fileSystem.Path.Combine(tmpDirectory.FullName, dbName);
        var destinationOptions = new SqliteConnectionStringBuilder
        {
            DataSource = destinationDbPath,
        };

        var source = new SqliteConnection(sourceOptions.ConnectionString);
        var destination = new SqliteConnection(destinationOptions.ConnectionString);

        await using (source)
        await using (destination)
        {
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);

            // https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.backupdatabase
            source.BackupDatabase(destination);
        }

        // Clearing the connection cache releases the file locks on the temporary databases.
        // !! resets ALL pools application-wide
        SqliteConnection.ClearPool(source);
        SqliteConnection.ClearPool(destination);

        // Copy the temporary backup to stream
        await using var stream = fileSystem.FileStream.New(destinationDbPath, FileMode.Open, FileAccess.Read, FileShare.None);
        await stream.CopyToAsync(destinationStream, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating sqlite backup for entry='{EntryKey}'")]
    private static partial void LogCreatingDatabaseEntry(ILogger logger, string entryKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created sqlite backup for entry='{EntryKey}' position={Bytes}")]
    private static partial void LogCreatedDatabaseEntry(ILogger logger, string entryKey, long bytes);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create sqlite backup for entry='{EntryKey}'")]
    private static partial void LogFailedToCreateDatabaseEntry(ILogger logger, Exception ex, string entryKey);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error creating sqlite backups")]
    private static partial void LogUnexpectedBackupSqliteError(ILogger logger, Exception ex);
}
