using Core.OS.Instance;
using Core.OS.Instance.HealthCheck;
using Core.Shared.Instance.HealthCheck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Instance.HealthCheck;

public sealed class InstanceHealthCheckPublisherTests
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
    public async Task Should_publish_instance_health_info_and_health_changed_event()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        await using var serviceProvider = SetupServiceProvider();
        var publisher = serviceProvider.GetRequiredService<InstanceHealthCheckPublisher>();
        SetupReadyInstance(instanceId);

        // Act
        await publisher.PublishAsync(
            new HealthReport(Substitute.For<IReadOnlyDictionary<string, HealthReportEntry>>(), HealthStatus.Unhealthy, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        // Assert
        _ = _mediatorMock.Received()
            .Publish(
                Arg.Is<InstanceHealthInfo>(message =>
                    message!.SenderInstanceId == instanceId && message.Status == HealthStatus.Unhealthy),
                Arg.Any<CancellationToken>());

        _ = _mediatorMock.Received()
            .Publish(
                Arg.Is<InstanceHealthChangedEvent>(message => message!.Status == HealthStatus.Unhealthy),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_publish_instance_health_info_only_when_no_status_change()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        await using var serviceProvider = SetupServiceProvider();
        var publisher = serviceProvider.GetRequiredService<InstanceHealthCheckPublisher>();
        SetupReadyInstance(instanceId);

        // Act
        await publisher.PublishAsync(
            new HealthReport(Substitute.For<IReadOnlyDictionary<string, HealthReportEntry>>(), HealthStatus.Healthy, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        // Assert
        _ = _mediatorMock.Received()
            .Publish(
                Arg.Is<InstanceHealthInfo>(message =>
                    message!.SenderInstanceId == instanceId &&
                    message.Status == HealthStatus.Healthy),
                TestContext.Current.CancellationToken);

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
