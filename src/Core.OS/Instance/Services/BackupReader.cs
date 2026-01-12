using System.IO.Abstractions;
using System.IO.Compression;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Sdk.SystemConfiguration.Contracts;

namespace Core.OS.Instance.Services;

public static class BackupReader
{
    public static async Task<BackupMetadata> GetBackupMetadata(IFileSystem fileSystem, string backupFilePath, CancellationToken cancellationToken = default)
    {
        await using var archiveStream = fileSystem.FileStream.New(backupFilePath, FileMode.Open, FileAccess.Read);
        return await GetBackupMetadata(archiveStream, cancellationToken);
    }

    public static async Task<BackupMetadata> GetBackupMetadata(Stream archiveStream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);
        return await GetBackupMetadata(archive, cancellationToken);
    }

    public static async Task<BackupMetadata> GetBackupMetadata(ZipArchive archive, CancellationToken cancellationToken = default)
    {
        var entry = archive.Entries.FirstOrDefault(k => k.Name == BackupBuilder.MetadataEntryName);
        if (entry is null)
            throw new InvalidOperationException("Could not find system configuration within backup.");

        return await entry.DeserializeEntry<BackupMetadata?>(cancellationToken)
            ?? throw new InvalidOperationException("Could not deserialize metadata from backup.");
    }

    public static IEnumerable<ZipArchiveEntry> GetSystemModuleEntry(Stream archiveStream)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);
        return GetModuleEntries(archive);
    }

    public static ZipArchiveEntry GetSystemModuleEntry(ZipArchive archive)
        => archive.Entries.First(k => k.Name == Sdk.Constants.SystemModuleId);

    public static void ExtractSystemModuleTo(IFileSystem fileSystem, string backupFilePath, string destinationPath)
    {
        using var archiveStream = fileSystem.FileStream.New(backupFilePath, FileMode.Open, FileAccess.Read);

        ExtractSystemModuleTo(archiveStream, destinationPath);
    }

    public static void ExtractSystemModuleTo(Stream archiveStream, string destinationPath)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);

        ExtractSystemModuleTo(archive, destinationPath);
    }

    /// <summary>
    /// Uses <see cref="ZipFile.ExtractToDirectory(System.IO.Stream,string)" /> to extract system module archive
    /// to <see cref="destinationPath"/>. It does not support IO.Abstractions and will extract the entries into
    /// the real filesystem.
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="destinationPath"></param>
    public static void ExtractSystemModuleTo(ZipArchive archive, string destinationPath)
    {
        var moduleEntry = GetSystemModuleEntry(archive);

        using var entryStream = moduleEntry.Open();
        ZipFile.ExtractToDirectory(entryStream, destinationPath);
    }

    public static async Task<SystemConfiguration?> GetSystemConfiguration(IFileSystem fileSystem, string backupFilePath, CancellationToken cancellationToken = default)
    {
        await using var archiveStream = fileSystem.FileStream.New(backupFilePath, FileMode.Open, FileAccess.Read);
        return await GetSystemConfiguration(archiveStream, cancellationToken);
    }

    public static async Task<SystemConfiguration?> GetSystemConfiguration(Stream archiveStream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);
        return await GetSystemConfiguration(archive, cancellationToken);
    }

    public static async Task<SystemConfiguration?> GetSystemConfiguration(ZipArchive archive, CancellationToken cancellationToken = default)
    {
        var entry = archive.Entries.FirstOrDefault(k => k.Name == nameof(SystemConfiguration));
        if (entry is null)
            throw new InvalidOperationException("Could not find system configuration within backup.");

        // if no HM is installed SystemConfiguration can be null
        return await entry.DeserializeEntry<SystemConfiguration?>(cancellationToken);
    }

    public static IEnumerable<ZipArchiveEntry> GetModuleEntries(IFileSystem fileSystem, string backupFilePath)
    {
        using var archiveStream = fileSystem.FileStream.New(backupFilePath, FileMode.Open, FileAccess.Read);
        return GetModuleEntries(archiveStream);
    }

    public static IEnumerable<ZipArchiveEntry> GetModuleEntries(Stream archiveStream)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);
        return GetModuleEntries(archive);
    }

    public static IEnumerable<ZipArchiveEntry> GetModuleEntries(ZipArchive archive)
        => archive.Entries.Where(k => k.Name.StartsWith(BackupBuilder.ModuleEntryPrefix, StringComparison.Ordinal));

    public static void ExtractModulesTo(IFileSystem fileSystem, string backupFilePath, string destinationPath, IReadOnlyCollection<string>? moduleNames = null)
    {
        using var archiveStream = fileSystem.FileStream.New(backupFilePath, FileMode.Open, FileAccess.Read);

        ExtractModulesTo(archiveStream, destinationPath, moduleNames);
    }

    public static void ExtractModulesTo(Stream archiveStream, string destinationPath, IReadOnlyCollection<string>? moduleNames = null)
    {
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);

        ExtractModulesTo(archive, destinationPath, moduleNames);
    }

    /// <summary>
    /// Uses <see cref="ZipFile.ExtractToDirectory(System.IO.Stream,string)" /> to extract module archive entries
    /// to <see cref="destinationPath"/>. It does not support IO.Abstractions and will extract the entries into
    /// the real filesystem.
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="destinationPath"></param>
    /// <param name="moduleNames"></param>
    private static void ExtractModulesTo(ZipArchive archive, string destinationPath, IReadOnlyCollection<string>? moduleNames = null)
    {
        var moduleEntries = GetModuleEntries(archive);

        foreach (var moduleEntry in moduleEntries)
        {
            // filter entries
            if (moduleNames?.Contains(moduleEntry.Name) == true)
                continue;

            using var entryStream = moduleEntry.Open();
            ZipFile.ExtractToDirectory(entryStream, destinationPath);
        }
    }
}
