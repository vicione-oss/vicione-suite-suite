using System.Globalization;
using System.IO.Abstractions;
using Core.OS.Instance.Extensions;
using Core.Shared.Persistence.Contracts;
using Microsoft.Extensions.Options;

namespace Core.OS.Instance.Services;

internal sealed class BackupStore(IOptions<InstanceOptions> instanceOptions, IFileSystem fileSystem, TimeProvider timeProvider) : IBackupStore
{
    private readonly string _directory = fileSystem.GetRootedBackupDirectory(instanceOptions.Value);
    private readonly Lock _lock = new();

    public Stream CreateBackupFile(out string filename)
    {
        lock (_lock)
        {
            filename = ToBackupFilename(timeProvider.GetUtcNow());
            var path = fileSystem.Path.Combine(_directory, filename);

            fileSystem.Directory.CreateDirectory(_directory);

            if (fileSystem.File.Exists(path))
                throw new InvalidOperationException($"Backup file '{filename}' already exists.");

            return fileSystem.File.Create(path);
        }
    }

    public Stream ReadBackupFile(string? filename = null)
    {
        lock (_lock)
        {
            var path = fileSystem.Path.Combine(_directory, filename ?? GetMostRecentBackupFilename());

            if (!fileSystem.File.Exists(path))
                throw new FileNotFoundException($"Backup file '{filename}' not found.", filename);

            return fileSystem.File.OpenRead(path);
        }
    }

    public IReadOnlyCollection<BackupFileInfo> GetBackupFiles()
    {
        lock (_lock)
        {
            var files = fileSystem.Directory.GetFiles(_directory, $"*{Shared.Constants.BackupFileExtension}", SearchOption.TopDirectoryOnly);
            return [.. files.Select(CreateBackupFileInfo)];
        }
    }

    private BackupFileInfo CreateBackupFileInfo(string filePath)
    {
        var fileInfo = fileSystem.FileInfo.New(filePath);
        return new BackupFileInfo(
            fileSystem.Path.GetFileName(filePath) ?? throw new InvalidOperationException("Failed to get the backup file name."),
            ParseBackupFilename(fileInfo),
            fileInfo.Length);
    }

    private string GetMostRecentBackupFilename()
    {
        var files = fileSystem.Directory.GetFiles(_directory, $"*{Shared.Constants.BackupFileExtension}", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
            throw new InvalidOperationException("No backup files found.");

        var mostRecentFile = files.OrderByDescending(fileSystem.File.GetLastWriteTime).First();
        return fileSystem.Path.GetFileName(mostRecentFile) ?? throw new InvalidOperationException("Failed to get the most recent backup file name.");
    }

    private static DateTimeOffset ParseBackupFilename(IFileInfo fileInfo)
    {
        var datePart = fileInfo.Name[..fileInfo.Name.IndexOf('.', StringComparison.InvariantCulture)];

        if (DateTimeOffset.TryParseExact(datePart, "yyyy-MM-dd_HH-mm-ss", null, DateTimeStyles.AssumeUniversal, out var dateTime))
            return dateTime;

        return fileInfo.LastWriteTimeUtc;
    }

    private static string ToBackupFilename(DateTimeOffset dateTime)
        => $"{dateTime:yyyy-MM-dd_HH-mm-ss}{Shared.Constants.BackupFileExtension}";

    public void DeleteBackupFile(string filename)
    {
        lock (_lock)
        {
            var path = fileSystem.Path.Combine(_directory, filename);
            if (!fileSystem.File.Exists(path))
                return;

            fileSystem.File.Delete(path);
        }
    }

    public BackupFileInfo GetLatestBackup()
    {
        var fileName = GetMostRecentBackupFilename();
        var filePath = fileSystem.Path.Combine(_directory, fileName);

        return CreateBackupFileInfo(filePath);
    }
}
