
namespace Core.Shared.HostManagement;

public enum MockPipeClientDataSource
{
    /// <summary>
    /// Initial data from <see cref="MockPipeClientOptions.SystemConfiguration"/>.
    /// </summary>
    SystemConfiguration,

    /// <summary>
    /// Initial data from <see cref="MockPipeClientOptions.SystemConfigurationJsonFile"/>.
    /// </summary>
    SystemConfigurationJsonFile,

    /// <summary>
    /// Initial data from an embedded resource.
    /// </summary>
    SystemConfigurationEmbedded
}
