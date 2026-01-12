using System.IO.Abstractions;
using System.IO.Compression;
using Core.Shared.Persistence.Contracts;
using Microsoft.Data.Sqlite;

namespace Core.OS.Instance.Services;

internal partial class BackupBuilder
{
    public static async Task<List<BackupEntrySummary>?> BackupSqliteDatabases(ZipArchive moduleArchive, IFileSystem fileSystem, string moduleWorkspace, Dictionary<string, string> databaseFiles, CancellationToken cancellationToken = default)
    {
        if (databaseFiles.Count == 0)
            return null;

        var result = new List<BackupEntrySummary>();

        // We can't backup the sqlite database directly to a stream.
        // We can't just copy the db files, because it might corrupt the data
        // Therefore we need to backup databases to new temporary files and add them to the archive instead.
        var tmpDirectory = fileSystem.Directory.CreateTempSubdirectory();

        foreach (var databaseFile in databaseFiles)
        {
            var dbSummary = new BackupEntrySummary
            {
                Name = databaseFile.Key,
                EntryName = databaseFile.Key,
            };

            try
            {
                var newEntry = moduleArchive.CreateEntry(databaseFile.Key);
                await using var destStream = newEntry.Open();
                var sourceDbPath = fileSystem.Path.Combine(moduleWorkspace, databaseFile.Value);

                await BackupSqliteDatabase(fileSystem, sourceDbPath, tmpDirectory, destStream, cancellationToken);
                dbSummary.ArchiveLength = destStream.Position;
            }
            catch (Exception e)
            {
                // todo log things
                dbSummary.Error = e.Message;
            }

            result.Add(dbSummary);
        }

        // clear the temporary ones
        try
        {
            fileSystem.Directory.Delete(tmpDirectory.FullName, true);
        }
        catch (Exception)
        {
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

        await using (var source = new SqliteConnection(sourceOptions.ConnectionString))
        {
            await using var destination = new SqliteConnection(destinationOptions.ConnectionString);
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);

            // Perform the backup writing to destination
            // See https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.backupdatabase?view=msdata-sqlite-9.0.0 about backup a sqlite database
            source.BackupDatabase(destination);
        }

        // we need to clear the connection cache to release the file locks from our temporary databases
        SqliteConnection.ClearAllPools();

        // Copy the temporary backup to stream
        await using var stream = fileSystem.FileStream.New(destinationDbPath, FileMode.Open, FileAccess.Read, FileShare.None);
        await stream.CopyToAsync(destinationStream, cancellationToken);
        stream.Close();
    }
}
