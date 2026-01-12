using Core.OS.Instance;
using Core.OS.Instance.HealthCheck;
using Core.Shared.Instance.HealthCheck;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.HealthCheck;

public class InstanceHealthCheckPublisherTests
{
    private readonly ILocalInstanceInformationProvider _instanceInformationMock = Substitute.For<ILocalInstanceInformationProvider>();
    private readonly ILogger<InstanceHealthCheckPublisher> _loggerMock = Substitute.For<ILogger<InstanceHealthCheckPublisher>>();
    private readonly ISuiteMediator _mediatorMock = Substitute.For<ISuiteMediator>();

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .AddSingleton(_instanceInformationMock)
            .AddSingleton(_mediatorMock)
            .AddSingleton(_loggerMock)
            .AddSingleton<InstanceHealthCheckPublisher>()
            .BuildServiceProvider();

    [Fact]
    public async Task PublishInstanceHealthInfoAndHealthChangedEvent()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        await using var serviceProvider = SetupServiceProvider();
        var publisher = serviceProvider.GetRequiredService<InstanceHealthCheckPublisher>();
        SetupReadyInstance(instanceId);

        // Act
        await publisher.PublishAsync(
            new HealthReport(Substitute.For<IReadOnlyDictionary<string, HealthReportEntry>>(), HealthStatus.Unhealthy, TimeSpan.Zero),
            CancellationToken.None);

        // Assert
        _ = _mediatorMock.Received()
            .Publish(
                Arg.Is<InstanceHealthInfo>(message =>
                    message.SenderInstanceId == instanceId && message.Status == HealthStatus.Unhealthy),
                Arg.Any<CancellationToken>());

        _ = _mediatorMock.Received()
            .Publish(
                Arg.Is<InstanceHealthChangedEvent>(message => message.Status == HealthStatus.Unhealthy),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishInstanceHealthInfoOnlyIfNoChange()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        await using var serviceProvider = SetupServiceProvider();
        var publisher = serviceProvider.GetRequiredService<InstanceHealthCheckPublisher>();
        SetupReadyInstance(instanceId);

        // Act
        await publisher.PublishAsync(
            new HealthReport(Substitute.For<IReadOnlyDictionary<string, HealthReportEntry>>(), HealthStatus.Healthy, TimeSpan.Zero),
            CancellationToken.None);

        // Assert
        _ = _mediatorMock.Received()
            .Publish(
                Arg.Is<InstanceHealthInfo>(message =>
                    message.SenderInstanceId == instanceId &&
                    message.Status == HealthStatus.Healthy),
                CancellationToken.None);

        _mediatorMock.ReceivedCalls().Should().HaveCount(1);
    }

    private void SetupReadyInstance(Guid instanceId, InstanceType type = InstanceType.Slave)
    {
        var info = new TestInstanceInformation
        {
            Id = instanceId,
            Type = type,
            Name = "Test",
        };

        _instanceInformationMock.Local
            .Returns(info);
    }
}
