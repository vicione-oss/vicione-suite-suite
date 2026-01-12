
namespace Core.Shared.HostManagement;

/// <summary>
/// Possible data sources for MockPipeClient
/// </summary>
public enum MockPipeClientDataSource
{
    /// <summary>
    /// MockPipeClient should read initial data from <see cref="MockPipeClientOptions.SystemConfiguration"/>
    /// </summary>
    SystemConfiguration,

    /// <summary>
    /// MockPipeClient should read initial data from <see cref="MockPipeClientOptions.SystemConfigurationJsonFile"/>
    /// </summary>
    SystemConfigurationJsonFile,

    /// <summary>
    /// MockPipeClient should read initial data from embedded resource
    /// </summary>
    SystemConfigurationEmbedded
}
