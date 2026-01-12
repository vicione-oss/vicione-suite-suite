using Core.Shared;
using Sdk.Backend.Persistence;

namespace Core.OS.Persistence;

public sealed class MasterDbConnectionStringProvider(IConfiguration config) : IMasterDbConnectionStringProvider
{
    public string ConnectionString { get; } = config.GetConnectionString("Postgres")
                           ?? throw new ConfigurationException("ConnectionStrings:Postgres");
}
