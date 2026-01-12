using System.IO.Abstractions;
using System.Text.Json;
using Core.OS.Modules;
using Sdk.Backend.Modules;
using Sdk.Messaging;

namespace Core.OS.Persistence;

/// <summary>
/// only available on master!
/// todo: extend ApplicationDbContext and implement the repository against the db
/// </summary>
internal sealed class InstanceConfigurationRepository(IWorkspaceProvider<SystemBackendModule> workspaceService,
                                                      IFileSystem fileSystem,
                                                      ILogger<InstanceConfigurationRepository> logger) : IInstanceConfigurationRepository
{
    private readonly IWorkspaceProvider<SystemBackendModule> _workspaceService = workspaceService;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger<InstanceConfigurationRepository> _logger = logger;

    public async Task<IConfiguration> StoreConfiguration(Guid instanceId, KeyValuePair<string, string?>[] configuration)
    {
        var builder = new ConfigurationBuilder();

        try
        {
            var targetFolder = GetInstanceConfigDirectory();
            if (!_fileSystem.Directory.Exists(targetFolder))
                _fileSystem.Directory.CreateDirectory(targetFolder);

            var instanceConfigName = GetInstanceConfigFileName(instanceId);
            var stream = _fileSystem.FileStream.New(instanceConfigName, new FileStreamOptions()
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            });
            await JsonSerializer.SerializeAsync(stream, configuration, DefaultJsonSerializerSettings.Default);
            await stream.DisposeAsync();

            builder.AddInMemoryCollection(configuration);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to store config for instance {InstanceId}", instanceId);
        }

        return builder.Build();
    }

    public async Task<IConfiguration?> GetConfiguration(Guid instanceId)
    {
        try
        {
            var instanceConfigName = GetInstanceConfigFileName(instanceId);
            if (!_fileSystem.File.Exists(instanceConfigName))
                throw new FileNotFoundException($"Instance configuration {instanceConfigName}");

            await using var stream = _fileSystem.FileStream.New(instanceConfigName, new FileStreamOptions()
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
            var values = await JsonSerializer.DeserializeAsync<List<KeyValuePair<string, string?>>>(stream, DefaultJsonSerializerSettings.Default);

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get config for instance {InstanceId}", instanceId);
        }

        return null;
    }

    private string GetInstanceConfigDirectory() => Path.Combine(_workspaceService.Home, "instances");
    private string GetInstanceConfigFileName(Guid instanceId) => Path.Combine(GetInstanceConfigDirectory(), $"{instanceId}.json");
}
