using System.IO.Abstractions;
using System.IO.Compression;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Modules;
using Core.Shared.Persistence.Contracts;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Modules;
using Sdk.SystemConfiguration.Contracts;
using Sdk.SystemConfiguration.Requests;

namespace Core.OS.Instance.Services;

internal sealed partial class BackupBuilder
{
    public const string MetadataEntryName = "Metadata.json";
    public const string ModuleEntryPrefix = "Module-";
    private const string SqliteDbExtensions = ".db";

    private List<string>? _addModuleIds;
    private bool _addSystemModule;
    private bool _addSystemConfiguration;
    private string? _fileName;
    private bool _overrideExistingBackup;

    public BackupBuilder UseModuleBackup(params IReadOnlyCollection<string> moduleIds)
    {
        _addModuleIds ??= [];
        _addModuleIds.AddRange(moduleIds);
        _addSystemModule = true;
        return this;
    }

    public BackupBuilder UseBackupFileName(string backupFileName)
    {
        _fileName = backupFileName;
        return this;
    }

    public BackupBuilder UseSystemModuleBackup()
    {
        _addSystemModule = true;
        return this;
    }

    public BackupBuilder UseSystemConfigurationBackup()
    {
        _addSystemConfiguration = true;
        return this;
    }

    public BackupBuilder UseOverrideExistingBackup()
    {
        _overrideExistingBackup = true;
        return this;
    }

    public async Task<BackupSummary> BuildBackup(IServiceProvider services, string backupPath, CancellationToken cancellationToken)
    {
        var fileSystem = services.GetRequiredService<IFileSystem>();
        var destinationFilePath = EnsureDestinationFilePath(fileSystem, backupPath);

        await using var destinationFs = fileSystem.FileStream.New(destinationFilePath, FileMode.Create, FileAccess.Write);

        return await BuildBackup(services, destinationFs, cancellationToken);
    }

    public async Task<BackupSummary> BuildBackup(IServiceProvider services, Stream destinationStream, CancellationToken cancellationToken)
    {
        var metadata = CreateMetadataEntry(services);
        var summary = new BackupSummary
        {
            InstanceId = metadata.InstanceId,
            Name = metadata.Name,
            SdkVersion = metadata.SdkVersion,
            SuiteVersion = metadata.SuiteVersion,
            InstanceType = metadata.Type.ToString(),
        };

        try
        {
            using var zipArchive = new ZipArchive(destinationStream, ZipArchiveMode.Create, false);

            // system module is not an 'installed' module
            summary.SystemModule = await AddSystemModuleBackup(zipArchive, services, cancellationToken);

            // modules appdata compressed entries
            var moduleSummaries = await AddModuleBackupEntries(zipArchive, services, cancellationToken);
            summary.Modules.AddRange(moduleSummaries);
            metadata.Modules = moduleSummaries.Select(k => new ModuleBackupMetadata
            {
                Name = k.Name,
                Version = k.Version,
                EntryName = k.EntryName,
            })
            .ToList();

            // system configuration retrieved from HostManagement
            summary.SystemConfiguration = await AddSystemConfigurationBackupEntry(zipArchive, services, cancellationToken);

            // metadata of created backup
            await AddMetadataEntry(zipArchive, metadata, cancellationToken);
        }
        catch (Exception e)
        {
            summary.Error = e.Message;
        }

        return summary;
    }

    private string EnsureDestinationFilePath(IFileSystem fileSystem, string backupPath)
    {
        // could be null if not used at all
        if (_fileName is not null)
        {
            if (string.IsNullOrWhiteSpace(_fileName))
                throw new InvalidOperationException($"Use '{nameof(UseBackupFileName)}' of with filename containing only whitespaces is invalid");

            if (!_fileName.EndsWith(Shared.Constants.BackupFileExtension, StringComparison.OrdinalIgnoreCase))
                fileSystem.Path.ChangeExtension(_fileName, Shared.Constants.BackupFileExtension);
        }

        _fileName ??= $"suite-backup-{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}{Shared.Constants.BackupFileExtension}";
        var destinationFilePath = fileSystem.Path.GetFullPath(fileSystem.Path.Combine(backupPath, _fileName));

        if (fileSystem.File.Exists(destinationFilePath))
        {
            if (_overrideExistingBackup)
                fileSystem.File.Delete(destinationFilePath);
            else
                throw new InvalidOperationException($"Backup file already exists. Use `{nameof(UseOverrideExistingBackup)}` if you want to overwrite the backup file.");
        }

        return destinationFilePath;
    }

    private BackupMetadata CreateMetadataEntry(IServiceProvider services)
    {
        var informationProvider = services.GetRequiredService<ILocalInstanceInformationProvider>();

        // keep a separate class for it to stay independent
        return new()
        {
            InstanceId = informationProvider.Local.Id,
            SuiteVersion = informationProvider.Local.Version,
            SdkVersion = informationProvider.Local.SdkVersion,
            Name = informationProvider.Local.Name,
            Type = informationProvider.Local.Type,
        };
    }

    private Task AddMetadataEntry(ZipArchive zipArchive, BackupMetadata metadata, CancellationToken cancellationToken)
    {
        var entry = zipArchive.CreateEntry(MetadataEntryName, CompressionLevel.NoCompression);
        return entry.SerializeToEntry(metadata, cancellationToken);
    }

    private async Task<BackupModuleSummary?> AddSystemModuleBackup(ZipArchive zipArchive, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!_addSystemModule)
            return null;

        var workspaceManagement = services.GetRequiredService<IWorkspaceManagement>();
        var fileSystem = services.GetRequiredService<IFileSystem>();
        var instanceInfo = services.GetRequiredService<IInstanceInformationProvider>().Local;

        var systemHome = workspaceManagement.GetHomeDirectory(Shared.Constants.SystemModuleId);
        var systemMeta = new ModuleMetadata
        {
            Name = Shared.Constants.SystemModuleId,
            MinSuiteSdkVersion = instanceInfo.SdkVersion,
            Version = instanceInfo.Version,
        };

        return await AddModuleBackupEntry(zipArchive, fileSystem, Shared.Constants.SystemModuleId, systemMeta.Version, systemHome, false, cancellationToken);
    }

    private async Task<List<BackupModuleSummary>> AddModuleBackupEntries(ZipArchive zipArchive, IServiceProvider services, CancellationToken cancellationToken)
    {
        // early return so tests don't need to setup these services
        if (_addModuleIds is null)
            return [];

        var workspaceManagement = services.GetRequiredService<IWorkspaceManagement>();
        var metadataCache = services.GetRequiredService<IModuleMetadataCache>();
        var fileSystem = services.GetRequiredService<IFileSystem>();

        var options = new Dictionary<string, ModuleMetadata>();
        var result = new List<BackupModuleSummary>();
        var installedModules = await metadataCache.GetInstalledModuleMetadata(cancellationToken);

        // build called without module ids passed - take all
        if (_addModuleIds.Count == 0)
        {
            foreach (var module in installedModules)
            {
                options.Add(workspaceManagement.GetHomeDirectory(module.ModuleId), module.Metadata);
            }
        }
        else
        {
            // add only specified modules
            foreach (var moduleId in _addModuleIds)
            {
                // if the module id is not installed, we ignore it
                var metadataBundle = installedModules.FirstOrDefault(k => k.ModuleId == moduleId);
                if (metadataBundle is null)
                    continue;

                options.Add(workspaceManagement.GetHomeDirectory(moduleId), metadataBundle.Metadata);
            }
        }

        foreach (var option in options)
        {
            // add one entry per module
            var moduleSummary = await AddModuleBackupEntry(zipArchive, fileSystem, option.Value.Name, option.Value.Version, option.Key, true, cancellationToken);
            result.Add(moduleSummary);
        }

        return result;
    }

    private async Task<BackupModuleSummary> AddModuleBackupEntry(ZipArchive zipArchive, IFileSystem fileSystem, string moduleName, string moduleVersion, string moduleWorkspace, bool addPrefix, CancellationToken cancellationToken)
    {
        // wrap ModuleMetadata to stay independent of sdk changes here
        var prefix = addPrefix ? ModuleEntryPrefix : string.Empty;
        var summary = new BackupModuleSummary
        {
            Name = moduleName,
            Version = moduleVersion,
            EntryName = $"{prefix}{moduleName}",
        };

        try
        {
            // a lot of change will come to Zip* modules with .NET 10 
            // https://github.com/dotnet/core/blob/main/release-notes/10.0/preview/preview4/libraries.md#new-async-zip-apis

            // ZIP files are sequential: There’s no room in the file format to "delete" entries without rewriting the archive,
            // because the file table (central directory) is located at the end of the archive.
            // System.IO.Compression.ZipArchive does not support deletion in Update mode—only adding or replacing files.
            var entry = zipArchive.CreateEntry(summary.EntryName);
            await using var entryStream = entry.Open();
            using var moduleArchive = new ZipArchive(entryStream, ZipArchiveMode.Create, false);

            await AddModuleDirectoryToArchive(fileSystem, moduleArchive, moduleWorkspace, summary, cancellationToken);

            summary.ArchiveLength = entryStream.Position;
        }
        catch (Exception e)
        {
            summary.Error = e.Message;
        }

        return summary;
    }

    private async Task<BackupEntrySummary?> AddSystemConfigurationBackupEntry(ZipArchive zipArchive, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!_addSystemConfiguration)
            return null;

        using var scope = services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();
        var response = await mediator.Request<GetSystemConfiguration, GetSystemConfigurationResponse>(new(), cancellationToken);

        // if no HM is installed SystemConfiguration can be null
        return await AddSystemConfigurationBackupEntry(zipArchive, response.Configuration, cancellationToken);
    }

    private async Task<BackupEntrySummary?> AddSystemConfigurationBackupEntry(ZipArchive zipArchive, SystemConfiguration? configuration, CancellationToken cancellationToken)
    {
        if (!_addSystemConfiguration)
            return null;

        var summary = new BackupEntrySummary
        {
            Name = nameof(SystemConfiguration),
            EntryName = nameof(SystemConfiguration),
        };

        try
        {
            var entry = zipArchive.CreateEntry(summary.EntryName, CompressionLevel.Optimal);
            var position = await entry.SerializeToEntry(configuration, cancellationToken);

            summary.ArchiveLength = position;
        }
        catch (Exception e)
        {
            summary.Error = e.Message;
        }

        return summary;
    }
}
