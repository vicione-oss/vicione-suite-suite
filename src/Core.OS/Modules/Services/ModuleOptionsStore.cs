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
        // the context contains all modules (backend|client) to be loaded        
        HashSet<string> handled = [];

        foreach (var moduleContext in context.Modules)
        {
            // handle client or backend modules that share same module id only once
            if (handled.Contains(moduleContext.ModuleId))
                continue;

            // ignore our sample modules - they don't have metadata
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
            // without metadata we can't validate anything
            var metadata = await LoadMetadataOptions(moduleContext, cancellationToken);
            if (metadata is null || metadata.Options is null)
                return;

            // this will throw if the json stream is invalid
            var configBuilder = new ConfigurationBuilder();

            // add the module json if available
            if (!AddModuleSettingsJson(moduleContext, configBuilder))
            {
                // here we have no settings yet so we take the ones from metadata
                var defaultOptions = await StoreDefaultOptions(metadata, cancellationToken);

                configBuilder.AddInMemoryCollection(defaultOptions.AsConfiguration(moduleContext.ModuleId));
            }

            configBuilder
                .AddEnvironmentVariables()
                .AddUserSecrets<Program>();

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

    private bool AddModuleSettingsJson(ModuleDependencyContext moduleContext, IConfigurationBuilder configBuilder)
    {
        // try to get existing settings
        ArgumentException.ThrowIfNullOrEmpty(moduleContext.ModuleId, nameof(moduleContext.ModuleId));

        var moduleAppDataPath = fileSystem.CreateModuleAppDataDirectory(_instanceOptions, moduleContext.ModuleId);
        var moduleSettingsPath = fileSystem.Path.Combine(moduleAppDataPath, ModuleSettingsFileName);

        // we have no json settings - continue with metadata
        if (!fileSystem.Path.Exists(moduleSettingsPath))
            return false;

        // if we have a valid settings json we try to build a config with it
        using var fs = fileSystem.FileStream.New(moduleSettingsPath, new FileStreamOptions()
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
        });

        // this will throw if the json stream is invalid
        configBuilder.AddJsonStream(fs);
        return true;
    }

    private static void ThrowOnMissingOptions(ModuleMetadata metadata, IConfiguration configuration)
    {
        // now we need the metadata of the module
        if (metadata.Options is null)
            return;

        // no required option, no validation
        if (metadata.Options.All(k => !k.IsRequired))
            return;

        // special case now - we have required options but no settings.json file here
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

        // now we need the metadata of the module
        return await fileSystem.DeserializeModuleMetadata(moduleContext, cancellationToken);
    }

    public async Task<IConfiguration?> LoadJsonConfiguration(string moduleId, CancellationToken cancellationToken = default)
    {
        var configFilePath = CreateModuleWorkspaceConfigFilePath(moduleId);

        // module does not have settings
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

        // to be able to restore a valid configuration...
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

        // to be able to restore a valid configuration...
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

        // store the defaults in module workspace
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

        await using var fs = fileSystem.FileStream.New(configFilePath, new FileStreamOptions()
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        });

        // we need to tranform IConfiguration to be able to store it
        //https://stackoverflow.com/questions/62713523/using-system-text-json-to-serialize-an-iconfiguration-back-to-json
        var dictionary = configuration.AsEnumerable().ToDictionary(c => c.Key, c => c.Value);

        await JsonSerializer.SerializeAsync(fs, dictionary, ModuleSerializerOptions.GetOptions(), cancellationToken: cancellationToken);
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
