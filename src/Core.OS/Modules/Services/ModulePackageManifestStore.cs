using System.IO.Abstractions;
using System.Text.Json;
using Core.OS.Instance;
using Core.OS.Modules.Extensions;
using Microsoft.Extensions.Options;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Services;

public partial class ModulePackageManifestStore(IFileSystem fileSystem, IOptions<InstanceOptions> instanceOptions, ILogger<ModulePackageManifestStore> logger) : IModulePackageManifestStore
{
    /// <summary>
    /// Static version of <see cref="ModulePackageManifestStore.Load"/> method, used in scenarios where dependency injection is not available (e.g. during instance initialization before the service container is built)
    /// </summary>    
    public static async Task<ModulePackageManifest> Load(IFileSystem fileSystem, InstanceOptions options, Serilog.ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await DeserializeModulePackageManifest(fileSystem, options, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to load module package manifest, returning empty manifest");
        }

        return new ModulePackageManifest
        {
            Packages = []
        };
    }

    public async Task<ModulePackageManifest> Load(CancellationToken cancellationToken)
    {
        try
        {
            return await DeserializeModulePackageManifest(fileSystem, instanceOptions.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            LogLoadManifestError(logger, ex);
        }

        return new ModulePackageManifest
        {
            Packages = []
        };
    }

    public async Task Store(ModulePackageManifest manifest, CancellationToken cancellationToken)
        => await StoreInternal(manifest, fileSystem, instanceOptions.Value, cancellationToken);

    public static async Task Store(ModulePackageManifest manifest, IFileSystem fileSystem, InstanceOptions options, CancellationToken cancellationToken)
        => await StoreInternal(manifest, fileSystem, options, cancellationToken);

    private static async Task StoreInternal(ModulePackageManifest manifest, IFileSystem fileSystem, InstanceOptions options, CancellationToken cancellationToken)
    {
        var packagesFilename = fileSystem.GetModuleVersionsFilePath(options);

        // Write updated packages file
        using var fileStream = fileSystem.FileStream.New(packagesFilename, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fileStream, manifest, DefaultJsonSerializerSettings.Default, cancellationToken);
    }

    private static async Task<ModulePackageManifest> DeserializeModulePackageManifest(IFileSystem fileSys, InstanceOptions options, CancellationToken cancellationToken)
    {
        var packagesFilename = fileSys.GetModuleVersionsFilePath(options);

        using var fs = fileSys.FileStream.New(packagesFilename, new FileStreamOptions()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });

        return await JsonSerializer.DeserializeAsync<ModulePackageManifest>(fs, DefaultJsonSerializerSettings.Default, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize module package manifest");
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load module package manifest, returning empty manifest")]
    private static partial void LogLoadManifestError(ILogger logger, Exception ex);
}
