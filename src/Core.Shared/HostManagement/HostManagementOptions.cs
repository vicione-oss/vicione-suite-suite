using System.IO.Pipes;

namespace Core.Shared.HostManagement;

public sealed class HostManagementOptions
{
    public const string ConfigSection = "HostManagement";
    private const string DefaultPipeName = "HostPipe";
    private const string DefaultSshServiceName = "ssh.service";

    public MockPipeClientOptions? MockClient { get; set; }
    public string PipeName { get; set; } = DefaultPipeName;
    public string SshServiceName { get; set; } = DefaultSshServiceName;
    public PipeOptions? PipeOptions { get; set; }

    /// <summary>
    /// Set the lifetime of the package cache in ms - after this period the cache gets invalid and
    /// will be refreshed on next request
    /// </summary>
    public long ConfigurationCacheLifetimeMs { get; set; } = 30_000;
}
