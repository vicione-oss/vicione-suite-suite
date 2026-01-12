using Core.OS.HostManagement;
using Core.OS.HostManagement.Consumers;
using Core.OS.Tests.HostManagement.Extensions;
using Core.Shared.HostManagement;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.HostManagement.Consumers;

public sealed class InstallSuiteVersionConsumerTests
{
    [Fact]
    public async Task Should_send_install_started_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<InstallSuiteVersionConsumer>();
            cfg.AddMockPipeClientSystemConfiguration();
        });
        var command = new InstallSuiteVersion("1.0.0");

        // Act
        await tester.TestCommand<InstallSuiteVersion, InstallSuiteVersionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<InstallSuiteVersionStarted>()).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_error_event_on_exception()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg =>
        {
            cfg.AddConsumer<InstallSuiteVersionConsumer>();
            cfg.AddSingleton(Substitute.For<IPipeClient>());
        });

        var command = new InstallSuiteVersion("1.0.0");

        // Act
        await tester.TestCommand<InstallSuiteVersion, InstallSuiteVersionConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<InstallSuiteVersionError>()).Should().BeTrue();
    }
}
