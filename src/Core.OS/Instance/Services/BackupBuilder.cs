using System.IO.Abstractions;
using System.IO.Compression;
using Core.OS.Instance.Contracts;
using Core.OS.Instance.Extensions;
using Core.OS.Modules;
using Core.OS.Modules.Contracts;
using Core.Shared.HostManagement;
using Core.Shared.Persistence.Contracts;
using HostManagement.Shared.Contracts;
using Sdk.Backend.Messaging;
using Sdk.Modules;

namespace Core.OS.Instance.Services;

internal sealed partial class BackupBuilder(IFileSystem fileSystem, ILocalInstanceInformationProvider instanceInformationProvider, ILogger<BackupBuilder> logger)
{
    public const string MetadataEntryName = "Metadata.json";
    public const string ModuleEntryPrefix = "Module-";
    private const string SqliteDbExtensions = ".db";

    private List<string>? _addModuleIds;
    private bool _addSystemConfiguration;
    private string _fileName = $"suite-backup-{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}{Shared.Constants.BackupFileExtension}";
    private bool _overrideExistingBackup;
    private IModuleMetadataProvider? _metadataProvider;
    private IWorkspaceManagement? _workspaceManagement;
    private IServiceProvider? _serviceProvider;

    public BackupBuilder UseModuleBackup(IModuleMetadataProvider metadataProvider, IWorkspaceManagement workspaceManagement, params IEnumerable<string> moduleIds)
    {
        _addModuleIds ??= [];
        _addModuleIds.AddRange(moduleIds);
        _metadataProvider = metadataProvider;
        _workspaceManagement = workspaceManagement;
        return this;
    }

    public BackupBuilder UseBackupFileName(string backupFileName)
    {
        _fileName = backupFileName;

        if (!_fileName.EndsWith(Shared.Constants.BackupFileExtension, StringComparison.OrdinalIgnoreCase))
            _fileName = fileSystem.Path.ChangeExtension(_fileName, Shared.Constants.BackupFileExtension);

        return this;
    }

    public BackupBuilder UseSystemConfigurationBackup(IServiceProvider serviceProvider)
    {
        _addSystemConfiguration = true;
        _serviceProvider = serviceProvider;
        return this;
    }

    public BackupBuilder UseOverrideExistingBackup()
    {
        _overrideExistingBackup = true;
        return this;
    }

    public async Task<BackupSummary> BuildBackup(string backupPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_fileName))
            throw new InvalidOperationException($"Use '{nameof(UseBackupFileName)}' with filename containing only whitespaces is invalid");

        if (!fileSystem.Directory.Exists(backupPath))
            throw new DirectoryNotFoundException($"Backup directory '{backupPath}' does not exist.");

        var destinationFilePath = fileSystem.Path.GetFullPath(fileSystem.Path.Combine(backupPath, _fileName));
        if (fileSystem.File.Exists(destinationFilePath))
        {
            if (_overrideExistingBackup)
                fileSystem.File.Delete(destinationFilePath);
            else
                throw new InvalidOperationException($"Backup file already exists. Use `{nameof(UseOverrideExistingBackup)}` if you want to overwrite the backup file.");
        }

        await using var destinationFs = fileSystem.FileStream.New(destinationFilePath, FileMode.Create, FileAccess.Write);

        return await BuildBackup(destinationFs, cancellationToken);
    }

    public async Task<BackupSummary> BuildBackup(Stream destinationStream, CancellationToken cancellationToken)
    {
        var metadata = CreateMetadataEntry(instanceInformationProvider);
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
            LogCreatingBackupArchive(logger, summary.InstanceId, summary.InstanceType, summary.SuiteVersion);

            using var zipArchive = new ZipArchive(destinationStream, ZipArchiveMode.Create, false);

            // modules appdata compressed entries
            if (_addModuleIds is not null)
            {
                // system module is not an 'installed' module
                summary.SystemModule = await AddSystemModuleBackup(zipArchive, _workspaceManagement!, cancellationToken);

                var moduleSummaries = await AddModuleBackupEntries(zipArchive, _metadataProvider!, _workspaceManagement!, [.. _addModuleIds], cancellationToken);
                summary.Modules.AddRange(moduleSummaries);
                metadata.Modules = [.. moduleSummaries.Select(k => new ModuleBackupMetadata
                {
                    Name = k.Name,
                    Version = k.Version,
                    EntryName = k.EntryName,
                })];
            }

            // system configuration retrieved from HostManagement
            if (_addSystemConfiguration)
            {
                summary.SystemConfiguration = await AddSystemConfigurationBackupEntry(zipArchive, _serviceProvider!, cancellationToken);
            }

            // metadata of created backup
            await AddMetadataEntry(zipArchive, metadata, cancellationToken);
        }
        catch (Exception e)
        {
            LogUnexpectedError(logger, e);

            summary.Error = e.Message;
        }

        return summary;
    }

    private static BackupMetadata CreateMetadataEntry(ILocalInstanceInformationProvider informationProvider) =>
        // keep a separate class for it to stay independent
        new()
        {
            InstanceId = informationProvider.Local.Id,
            SuiteVersion = informationProvider.Local.Version,
            SdkVersion = informationProvider.Local.SdkVersion,
            Name = informationProvider.Local.Name,
            Type = informationProvider.Local.Type,
        };

    private static Task<long> AddMetadataEntry(ZipArchive zipArchive, BackupMetadata metadata, CancellationToken cancellationToken)
    {
        var entry = zipArchive.CreateEntry(MetadataEntryName, CompressionLevel.NoCompression);
        return entry.SerializeToEntry(metadata, cancellationToken);
    }

    private async Task<BackupModuleSummary?> AddSystemModuleBackup(ZipArchive zipArchive, IWorkspaceManagement workspaceManagement, CancellationToken cancellationToken)
    {
        var instanceInfo = instanceInformationProvider.Local;
        var systemHome = workspaceManagement.GetHomeDirectory(Shared.Constants.SystemModuleId);
        var systemMeta = new ModuleMetadata
        {
            Name = Shared.Constants.SystemModuleId,
            MinSuiteSdkVersion = instanceInfo.SdkVersion,
            Version = instanceInfo.Version,
        };

        LogAddingSystemModule(logger);

        var moduleEntry = new ModuleEntry(systemMeta.Name, systemMeta.Version, systemHome, false);

        return await AddModuleBackupEntry(zipArchive, fileSystem, moduleEntry, logger, cancellationToken);
    }

    private async Task<List<BackupModuleSummary>> AddModuleBackupEntries(ZipArchive zipArchive, IModuleMetadataProvider metadataProvider, IWorkspaceManagement workspaceManagement, string[] moduleIds, CancellationToken cancellationToken)
    {
        // early return so tests don't need to setup these services
        var options = new Dictionary<string, ModuleMetadata>();
        var result = new List<BackupModuleSummary>();
        var getOptions = new GetModuleMetadataOptions(true, false);
        var installedModules = await metadataProvider.GetModuleMetadata(getOptions, cancellationToken);

        // build called without module ids passed - take all
        if (moduleIds.Length == 0)
        {
            LogAddingInstalledModules(logger);

            foreach (var module in installedModules)
            {
                options.Add(workspaceManagement.GetHomeDirectory(module.ModuleId), module.Metadata);
            }
        }
        else
        {
            LogAddingExplicitModules(logger);

            // add only specified modules
            foreach (var moduleId in moduleIds)
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
            LogAddingModuleOptions(logger, option.Key);

            // add one entry per module
            var moduleEntry = new ModuleEntry(option.Value.Name, option.Value.Version, option.Key, true);
            var moduleSummary = await AddModuleBackupEntry(zipArchive, fileSystem, moduleEntry, logger, cancellationToken);
            result.Add(moduleSummary);
        }

        return result;
    }

    private static async Task<BackupModuleSummary> AddModuleBackupEntry(ZipArchive zipArchive, IFileSystem fileSystem, ModuleEntry entryOptions, ILogger logger, CancellationToken cancellationToken)
    {
        // wrap ModuleMetadata to stay independent of sdk changes here
        var prefix = entryOptions.AddPrefix ? ModuleEntryPrefix : string.Empty;
        var summary = new BackupModuleSummary
        {
            Name = entryOptions.ModuleName,
            Version = entryOptions.ModuleVersion,
            EntryName = $"{prefix}{entryOptions.ModuleName}",
        };

        try
        {
            // ZIP files are sequential: There’s no room in the file format to "delete" entries without rewriting the archive,
            // because the file table (central directory) is located at the end of the archive.
            // System.IO.Compression.ZipArchive does not support deletion in Update mode—only adding or replacing files.
            var entry = zipArchive.CreateEntry(summary.EntryName);
            await using var entryStream = await entry.OpenAsync(cancellationToken);
            using var moduleArchive = new ZipArchive(entryStream, ZipArchiveMode.Create, false);

            await AddModuleDirectoryToArchive(fileSystem, moduleArchive, entryOptions.ModuleWorkspace, summary, logger, cancellationToken);

            summary.ArchiveLength = entryStream.Position;
        }
        catch (Exception e)
        {
            LogUnexpectedModuleEntryError(logger, e, summary.Name, summary.Version);

            summary.Error = e.Message;
        }

        return summary;
    }

    private record ModuleEntry(string ModuleName, string ModuleVersion, string ModuleWorkspace, bool AddPrefix);


    private async Task<BackupEntrySummary?> AddSystemConfigurationBackupEntry(ZipArchive zipArchive, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();

        var response = await mediator.Request<GetHostMgmtSystemConfiguration, GetHostMgmtSystemConfigurationResponse>(new(), cancellationToken);
        if (response.Configuration is null)
            throw new InvalidOperationException("Failed to retrieve system configuration from HostManagement. Response was successful but configuration was null.");

        // if no HM is installed SystemConfiguration can be null
        return await AddSystemConfigurationBackupEntry(zipArchive, response.Configuration, cancellationToken);
    }

    private async Task<BackupEntrySummary?> AddSystemConfigurationBackupEntry(ZipArchive zipArchive, SystemConfiguration configuration, CancellationToken cancellationToken)
    {
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
            LogUnexpectedSystemConfigurationEntryError(logger, e);

            summary.Error = e.Message;
        }

        return summary;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating backup archive for instance='{InstanceId}' type='{InstanceType}' version='{Version}'")]
    private static partial void LogCreatingBackupArchive(ILogger logger, Guid instanceId, string instanceType, string version);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding system module to archive")]
    private static partial void LogAddingSystemModule(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding installed modules to archive")]
    private static partial void LogAddingInstalledModules(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding explicit modules to archive")]
    private static partial void LogAddingExplicitModules(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Adding module='{ModuleId}' options to archive")]
    private static partial void LogAddingModuleOptions(ILogger logger, string moduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error on creating backup archive module='{Name}' version='{Version}' entry")]
    private static partial void LogUnexpectedModuleEntryError(ILogger logger, Exception ex, string name, string? version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error on creating backup archive system configuration entry")]
    private static partial void LogUnexpectedSystemConfigurationEntryError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error on creating backup archive")]
    private static partial void LogUnexpectedError(ILogger logger, Exception ex);
}
