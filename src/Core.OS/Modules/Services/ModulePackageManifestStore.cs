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
    public static async Task<ModulePackageManifest> Load(IFileSystem fileSystem, InstanceOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await DeserializeModulePackageManifest(fileSystem, options, cancellationToken);
        }
        catch (Exception ex)
        {
            // Do NOT mask corruption as an empty manifest: persisting an empty set would silently
            // wipe the installed-module state. Surface the error so the startup recovery pipeline
            // can back up the corrupt file and reseed it.
            LogLoadManifestError(logger, ex);
            throw;
        }
    }

    public Task<ModulePackageManifest> Load(CancellationToken cancellationToken)
        => Load(fileSystem, instanceOptions.Value, logger, cancellationToken);

    public async Task Store(ModulePackageManifest manifest, CancellationToken cancellationToken)
        => await StoreInternal(manifest, fileSystem, instanceOptions.Value, cancellationToken);

    public static async Task Store(ModulePackageManifest manifest, IFileSystem fileSystem, InstanceOptions options, CancellationToken cancellationToken)
        => await StoreInternal(manifest, fileSystem, options, cancellationToken);

    private static async Task StoreInternal(ModulePackageManifest manifest, IFileSystem fileSystem, InstanceOptions options, CancellationToken cancellationToken)
    {
        var packagesFilename = fileSystem.GetModuleVersionsFilePath(options);

        // Write updated packages file atomically so a crash mid-write can't corrupt the manifest.
        await fileSystem.WriteFileAtomic(
            packagesFilename,
            stream => JsonSerializer.SerializeAsync(stream, manifest, DefaultJsonSerializerSettings.Default, cancellationToken),
            cancellationToken);
    }

    private static async Task<ModulePackageManifest> DeserializeModulePackageManifest(IFileSystem fileSys, InstanceOptions options, CancellationToken cancellationToken)
    {
        var packagesFilename = fileSys.GetModuleVersionsFilePath(options);

        // A missing file is legitimate (fresh install / after recovery reseeded it) and yields an
        // empty manifest. An existing but unreadable file is treated as corruption and surfaced.
        if (!fileSys.File.Exists(packagesFilename))
        {
            return new ModulePackageManifest
            {
                Packages = []
            };
        }

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load module package manifest from a file that exists but is unreadable")]
    private static partial void LogLoadManifestError(ILogger logger, Exception ex);
}
