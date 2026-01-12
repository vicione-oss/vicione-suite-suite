using System.IO.Abstractions;
using System.Text.Json;
using Core.Module;
using Core.OS.Instance;
using Core.OS.Modules.Extensions;
using Sdk.Messaging;
using Sdk.Modules;

namespace Core.OS.Modules.Services;

internal class ModuleManifestProvider(IFileSystem fileSystem, InstanceOptions instanceOptions) : IModuleManifestProvider
{
    private readonly InstanceOptions _instanceOptions = instanceOptions;
    private ModulePackageManifest? _manifest;

    public async Task<ModulePackageManifest> LoadPackageManifest(Serilog.ILogger logger, CancellationToken cancellationToken = default)
    {
        if (_manifest is not null)
            return _manifest;

        var modulesFilePath = fileSystem.GetModuleVersionsFilePath(_instanceOptions);

        try
        {
            if (!fileSystem.Path.Exists(modulesFilePath))
                throw new InvalidOperationException($"Module versions file can't be found '{modulesFilePath}'");

            using var fs = fileSystem.FileStream.New(modulesFilePath, new FileStreamOptions()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });

            _manifest = await JsonSerializer.DeserializeAsync<ModulePackageManifest>(fs, DefaultJsonSerializerSettings.Default, cancellationToken);
            if (_manifest is null)
                throw new InvalidOperationException($"Failed to deserialize modules versions file '{modulesFilePath}'.");

            // can be empty or contain things
            return _manifest;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to load module versions file '{File}'", modulesFilePath);
        }

        // startup without modules - we should display the error in ui probably
        _manifest = new ModulePackageManifest();
        return _manifest;
    }

    public ModulePackageManifest GetManifest()
        => _manifest ?? throw new InvalidOperationException("ModuleManifest is not yet loaded.");

    public async Task UpdateManifestPackages(List<ModuleDependencyPackage> packages, CancellationToken cancellationToken = default)
    {
        var manifest = GetManifest();

        manifest.Packages.Clear();
        manifest.Packages.AddRange(packages);
        manifest.LastModified = DateTime.UtcNow;

        var modulesFilePath = fileSystem.GetModuleVersionsFilePath(_instanceOptions);

        if (!fileSystem.Path.Exists(modulesFilePath))
            throw new InvalidOperationException($"Module versions file can't be found '{modulesFilePath}'");

        await using var fs = fileSystem.FileStream.New(modulesFilePath, new FileStreamOptions()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        });

        await JsonSerializer.SerializeAsync(fs, manifest, ModuleSerializerOptions.GetOptions(), cancellationToken);
    }
}
