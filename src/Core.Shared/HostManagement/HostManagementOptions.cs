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
    /// The cache is invalidated after this period and refreshed on the next request, not on a timer.
    /// </summary>
    public long ConfigurationCacheLifetimeMs { get; set; } = 30_000;
}
