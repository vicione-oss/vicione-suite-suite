using System.Diagnostics;
using Core.OS.HostManagement;
using Core.Shared.HostManagement;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Enums;
using HostManagement.Shared.Communication.NamedPipe.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using HostManagementPipeClient = HostManagement.Shared.Communication.NamedPipe.Client.PipeClient;
using PipeClient = Core.OS.HostManagement.PipeClient;

namespace Core.OS.Tests.HostManagement;

public sealed class PipeClientTests
{
    private const int ConnectTimeoutMs = 200;

    [Fact]
    public async Task Should_fail_a_request_within_the_connect_timeout_when_no_server_listens()
    {
        // Arrange
        await using var pipeClient = CreatePipeClient();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var act = () => pipeClient.SendRequest(Topics.GetSupportedCapabilities, string.Empty, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        pipeClient.State.Should().Be(PipeState.NotOpened);
    }

    [Fact]
    public async Task Should_report_a_cancellation_while_connecting_as_cancellation()
    {
        // Arrange
        await using var pipeClient = CreatePipeClient();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var act = () => pipeClient.Connect(cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static PipeClient CreatePipeClient()
    {
        // A unique name, so that no HostManagement on the test machine answers.
        var options = new HostManagementOptions { PipeName = $"suite-test-{Guid.NewGuid():N}", ConnectTimeoutMs = ConnectTimeoutMs };

        return new PipeClient(Options.Create(options), NullLogger<HostManagementPipeClient>.Instance, new CallbackHandlerRegistry());
    }
}
