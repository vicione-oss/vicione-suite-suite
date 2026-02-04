using Core.OS.HostManagement;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Contracts;

namespace Core.OS.Tests.HostManagement;

/// <summary>
/// A wrapper around <see cref="MockPipeClient"/> to be used in tests. Will pretend to be a HostManagement pipe client.
/// </summary>
public sealed class TestPipeClient(MockPipeClient mockPipeClient) : IPipeClient
{
    public PipeState State => mockPipeClient.State;

    public void Dispose() => mockPipeClient.Dispose();

    public Task Connect(CancellationToken cancellationToken = default) => mockPipeClient.Connect(cancellationToken);

    public Task<string> SendRequest(string topic, string content, CancellationToken cancellationToken = default)
        => mockPipeClient.SendRequest(topic, content, cancellationToken);

    public static SystemConfiguration GetEmbeddedSystemConfiguration()
        => MockPipeClient.GetEmbeddedSystemConfiguration();
}
