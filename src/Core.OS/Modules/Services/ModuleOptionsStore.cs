using System.IO.Abstractions;
using System.Text.Json;
using Core.Module;
using Core.Module.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Modules.Extensions;
using Sdk.Modules;

namespace Core.OS.Modules.Services;

internal class ModuleOptionsStore(IFileSystem fileSystem, IConfiguration config) : IModuleOptionsStore
{
    public const string ModuleSettingsFileName = "module_settings.json";

    private readonly InstanceOptions _instanceOptions = config.GetInstanceOptions();

    public async Task ValidateModuleOptions(SuiteDependencyContext context, CancellationToken cancellationToken)
    {
        // The context holds every module to be loaded, backend and client alike.
        HashSet<string> handled = [];

        foreach (var moduleContext in context.Modules)
        {
            // A client and backend sharing a module id are handled once.
            if (handled.Contains(moduleContext.ModuleId))
                continue;

            // Sample modules have no metadata.
            if (ModuleConstants.SampleModuleIds.Contains(moduleContext.ModuleId))
                continue;

            await ValidateModuleOptionsInternal(moduleContext, cancellationToken);

            handled.Add(moduleContext.ModuleId);
        }

        context.ValidateDependencies();
    }

    private async Task ValidateModuleOptionsInternal(ModuleDependencyContext moduleContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Nothing can be validated without metadata.
            var metadata = await LoadMetadataOptions(moduleContext, cancellationToken);
            if (metadata is null || metadata.Options is null)
                return;

            // Throws on an invalid json stream.
            var configBuilder = new ConfigurationBuilder();

            // The module json is added when present.
            var moduleConfig = GetModuleSettingsJson(moduleContext);
            if (moduleConfig is not null)
            {
                configBuilder.AddConfiguration(moduleConfig);
            }
            else
            {
                // No settings yet, so the metadata defaults apply.
                var defaultOptions = await StoreDefaultOptions(metadata, cancellationToken);

                configBuilder.AddInMemoryCollection(defaultOptions.AsConfiguration(moduleContext.ModuleId));
            }

            // Sources are merged; env vars and user secrets may
            // contain required options that are not in the json
            var config = configBuilder
                .AddEnvironmentVariables()
                .AddUserSecrets<Program>()
                .Build();

            ThrowOnMissingOptions(metadata, config);
        }
        catch (JsonException ex)
        {
            moduleContext.StartupErrors.Add(new InvalidDataException("Invalid module metadata.", ex));
        }
        catch (Exception ex)
        {
            moduleContext.StartupErrors.Add(ex);
        }
    }

    private IConfiguration? GetModuleSettingsJson(ModuleDependencyContext moduleContext)
    {
        // Existing settings, if any.
        ArgumentException.ThrowIfNullOrEmpty(moduleContext.ModuleId, nameof(moduleContext.ModuleId));

        var moduleAppDataPath = fileSystem.CreateModuleAppDataDirectory(_instanceOptions, moduleContext.ModuleId);
        var moduleSettingsPath = fileSystem.Path.Combine(moduleAppDataPath, ModuleSettingsFileName);

        // No json settings, so metadata carries on.
        if (!fileSystem.Path.Exists(moduleSettingsPath))
            return null;

        // A valid settings json is built into a configuration.
        using var fs = fileSystem.FileStream.New(moduleSettingsPath, new FileStreamOptions()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
        });

        // Loads module_settings.json as configuration; throws when the json is invalid.
        return new ConfigurationBuilder()
                    .AddJsonStream(fs)
                    .Build();
    }

    private static void ThrowOnMissingOptions(ModuleMetadata metadata, IConfiguration configuration)
    {
        // The module metadata is needed here.
        if (metadata.Options is null)
            return;

        // Nothing to validate without a required option.
        if (metadata.Options.All(k => !k.IsRequired))
            return;

        // Required options but no settings.json.
        // the env_settings/user secrets might contain the required parameters
        var missing = metadata.GetMissingConfigurationKeys(configuration, true);
        if (missing.Count == 0)
            return;

        throw new InvalidOperationException($"Missing required settings: {string.Join(", ", missing)}");
    }

    private async Task<ModuleMetadata?> LoadMetadataOptions(ModuleDependencyContext moduleContext, CancellationToken cancellationToken = default)
    {
        if (moduleContext.IsDebugSource)
        {
            var debugMetadataPath = fileSystem.FindModuleMetadataPath(moduleContext.AssemblyFolder);
            if (string.IsNullOrEmpty(debugMetadataPath))
                return null;

            return await fileSystem.DeserializeModuleMetadata(debugMetadataPath, cancellationToken)
                ?? throw new InvalidOperationException($"Failed to load '{moduleContext.ModuleId}' debug metadata from '{debugMetadataPath}'.");
        }

        // The module metadata is needed here.
        return await fileSystem.DeserializeModuleMetadata(moduleContext, cancellationToken);
    }

    public async Task<IConfiguration?> LoadJsonConfiguration(string moduleId, CancellationToken cancellationToken = default)
    {
        var configFilePath = CreateModuleWorkspaceConfigFilePath(moduleId);

        // The module has no settings.
        if (!fileSystem.File.Exists(configFilePath))
            return null;

        await using var fs = fileSystem.FileStream.New(configFilePath, new FileStreamOptions()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });

        var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(fs, ModuleSerializerOptions.GetOptions(), cancellationToken: cancellationToken);

        // Kept so a valid configuration can be restored.
        return new ConfigurationBuilder()
            .AddInMemoryCollection(loaded)
            .Build();
    }

    private IConfiguration? LoadJsonConfigurationSync(string moduleId)
    {
        var configFilePath = CreateModuleWorkspaceConfigFilePath(moduleId);

        if (!fileSystem.File.Exists(configFilePath))
            return null;

        using var fs = fileSystem.FileStream.New(configFilePath, new FileStreamOptions()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.None,
        });

        var loaded = JsonSerializer.Deserialize<Dictionary<string, string?>>(fs, ModuleSerializerOptions.GetOptions());

        // Kept so a valid configuration can be restored.
        return new ConfigurationBuilder()
            .AddInMemoryCollection(loaded)
            .Build();
    }

    public async Task<Dictionary<string, string?>> LoadJsonDictionary(string moduleId, CancellationToken cancellationToken = default)
    {
        var configuration = await LoadJsonConfiguration(moduleId, cancellationToken);
        if (configuration is null)
            return [];

        return configuration.AsEnumerable().ToDictionary(c => c.Key, c => c.Value);
    }

    public Dictionary<string, string?> LoadDictionarySync(string moduleId)
    {
        var configuration = LoadJsonConfigurationSync(moduleId);
        if (configuration is null)
            return [];

        return configuration.AsEnumerable().ToDictionary(c => c.Key, c => c.Value);
    }

    private async Task<List<ModuleOptionDeclaration>> StoreDefaultOptions(ModuleMetadata metadata, CancellationToken cancellationToken = default)
    {
        if (metadata.Options is null)
            return [];

        var defaultOptions = metadata.Options
                .Where(k => !string.IsNullOrEmpty(k.DefaultValue))
                .ToList();

        foreach (var option in defaultOptions)
        {
            option.Value = option.DefaultValue;
        }

        // Defaults are stored in the module workspace.
        await Store(metadata.Name, defaultOptions, cancellationToken);

        return defaultOptions;
    }

    public async Task Store(string moduleId, List<ModuleOptionDeclaration> options, CancellationToken cancellationToken = default)
    {
        if (options.Count == 0)
            return;

        // todo - ensure types get stored correct, for now ok but array will come etc.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(options.AsConfiguration(moduleId))
            .Build();

        var configFilePath = CreateModuleWorkspaceConfigFilePath(moduleId);

        // IConfiguration has to be transformed before it can be stored.
        //https://stackoverflow.com/questions/62713523/using-system-text-json-to-serialize-an-iconfiguration-back-to-json
        var dictionary = configuration.AsEnumerable().ToDictionary(c => c.Key, c => c.Value);

        // Write atomically so a crash mid-write can't corrupt the module settings file.
        await fileSystem.WriteFileAtomic(
            configFilePath,
            stream => JsonSerializer.SerializeAsync(stream, dictionary, ModuleSerializerOptions.GetOptions(), cancellationToken: cancellationToken),
            cancellationToken);
    }

    private string CreateModuleWorkspaceConfigFilePath(string moduleId)
    {
        var appDataPath = fileSystem.GetRootedHomeDirectory(_instanceOptions);
        var moduleWorkspace = fileSystem.Path.Combine(appDataPath, moduleId);

        if (!fileSystem.Directory.Exists(moduleWorkspace))
            fileSystem.Directory.CreateDirectory(moduleWorkspace);

        return fileSystem.Path.Combine(moduleWorkspace, ModuleSettingsFileName);
    }
}
