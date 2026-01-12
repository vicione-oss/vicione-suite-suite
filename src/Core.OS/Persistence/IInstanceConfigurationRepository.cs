namespace Core.OS.Persistence;

internal interface IInstanceConfigurationRepository
{
    Task<IConfiguration> StoreConfiguration(Guid instanceId, KeyValuePair<string, string?>[] configuration);

    Task<IConfiguration?> GetConfiguration(Guid instanceId);
}
