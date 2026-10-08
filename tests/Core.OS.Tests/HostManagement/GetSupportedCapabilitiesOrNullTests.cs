using Core.OS.HostManagement;
using Core.OS.HostManagement.Extensions;
using Core.OS.Tests.HostManagement.Extensions;
using HostManagement.Shared.Capabilities;
using HostManagement.Shared.Communication;
using HostManagement.Shared.Communication.Capabilities;
using HostManagement.Shared.Communication.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;

namespace Core.OS.Tests.HostManagement;

public sealed class GetSupportedCapabilitiesOrNullTests
{
    private readonly IPipeClient _pipeClient = Substitute.For<IPipeClient>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    public GetSupportedCapabilitiesOrNullTests() => _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

    [Theory]
    [InlineData(CapabilityStatus.Enabled)]
    [InlineData(CapabilityStatus.Disabled)]
    public async Task Should_return_the_capabilities_reported_by_host_management(CapabilityStatus status)
    {
        // Arrange
        var capabilities = new SupportedCapabilities();
        capabilities.Topics.RestartService = status;
        capabilities.Settings.DNS.Hostname.Capability = status;
        _pipeClient.SetupGetSupportedCapabilitiesResult(OperationStatus.Success, capabilities);

        // Act
        var result = await _pipeClient.GetSupportedCapabilitiesOrNull(_logger, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Topics.RestartService.Should().Be(status);
        result.Settings.DNS.Hostname.Capability.Should().Be(status);
    }

    [Theory]
    [InlineData(OperationStatus.Warning)]
    [InlineData(OperationStatus.Error)]
    public async Task Should_return_null_and_log_a_warning_when_host_management_does_not_report_success(OperationStatus status)
    {
        // Arrange
        _pipeClient.SetupGetSupportedCapabilitiesResult(status, new SupportedCapabilities(), "Supported capabilities were not loaded.");

        // Act
        var result = await _pipeClient.GetSupportedCapabilitiesOrNull(_logger, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        LoggedWarnings().Should().Be(1);
    }

    [Fact]
    public async Task Should_return_null_and_log_a_warning_when_the_request_throws()
    {
        // Arrange
        _pipeClient.SendRequest(Topics.GetSupportedCapabilities, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed to connect to pipe 'test'."));

        // Act
        var result = await _pipeClient.GetSupportedCapabilitiesOrNull(_logger, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        LoggedWarnings().Should().Be(1);
    }

    [Fact]
    public async Task Should_not_swallow_a_cancellation()
    {
        // Arrange
        _pipeClient.SendRequest(Topics.GetSupportedCapabilities, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = () => _pipeClient.GetSupportedCapabilitiesOrNull(_logger, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(CapabilityStatus.Enabled, false)]
    [InlineData(CapabilityStatus.Disabled, true)]
    public async Task Should_report_whether_a_topic_is_disabled(CapabilityStatus status, bool expected)
    {
        // Arrange
        var capabilities = new SupportedCapabilities();
        capabilities.Topics.RestartService = status;
        _pipeClient.SetupGetSupportedCapabilitiesResult(OperationStatus.Success, capabilities);

        // Act
        var result = await _pipeClient.IsDisabled(topics => topics.RestartService, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_not_report_a_topic_as_disabled_when_the_capabilities_cannot_be_read()
    {
        // Arrange
        _pipeClient.SetupGetSupportedCapabilitiesResult(OperationStatus.Error, new SupportedCapabilities(), "Supported capabilities were not loaded.");

        // Act
        var result = await _pipeClient.IsDisabled(topics => topics.RestartService, _logger, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    private int LoggedWarnings()
        => _logger.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log) && call.GetArguments()[0] is LogLevel.Warning);
}
