using Core.OS.Instance.Consumers;
using Core.OS.Instance.Services;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.HealthCheck;
using Core.Shared.Instance.Requests;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Instance.Events;
using Sdk.Testing.Backend;
using Xunit;

namespace Core.OS.Tests.Instance.Consumers;

public class ClusterInformationConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly InMemoryClusterInformationProvider _informationProvider;
    private readonly ISuiteMediator _mediator = Substitute.For<ISuiteMediator>();
    private readonly InstanceInformation _instanceInfo;
    private readonly Guid _instanceId = Guid.NewGuid();

    public ClusterInformationConsumerTests()
    {
        _informationProvider = new InMemoryClusterInformationProvider(Substitute.For<ILogger<InMemoryClusterInformationProvider>>());
        _instanceInfo = new InstanceInformation
        {
            Id = _instanceId,
            Type = InstanceType.Standalone
        };

        _configureServices = cfg =>
        {
            cfg.AddConsumer<ClusterInformationConsumer>();
            cfg.AddSingleton(_informationProvider);
            cfg.AddSingleton(_mediator);
        };
    }

    [Fact]
    public async Task Should_consume_instance_administration_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var administratedEvent = new InstanceAdministrated(_instanceId, AdministrateInstanceAction.Delete, true);
        Guid? instanceId = null;

        await _informationProvider.AddNewInstance(_instanceInfo);

        _informationProvider.InstanceDeleted += (id) =>
        {
            instanceId = id;
            return Task.CompletedTask;
        };

        // Act
        await tester.TestEvent<InstanceAdministrated, ClusterInformationConsumer>(administratedEvent);

        // Assert
        instanceId.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_consume_instance_created_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var createdEvent = new InstanceCreated(_instanceId);
        IInstanceInformation? instance = null;

        _mediator.Request<GetInstances, GetInstancesResponse>(Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
            .Returns(new GetInstancesResponse([_instanceInfo]));

        _informationProvider.NewInstanceAdded += (info) =>
        {
            instance = info;
            return Task.CompletedTask;
        };

        // Act
        await tester.TestEvent<InstanceCreated, ClusterInformationConsumer>(createdEvent);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_consume_instance_health_info()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var healthEvent = new InstanceHealthInfo(_instanceId, DateTime.UtcNow, Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
        Guid? instanceId = null;

        await _informationProvider.AddNewInstance(_instanceInfo);
        _informationProvider.HealthStatusChanged += (id, status, time) => { instanceId = id; return Task.CompletedTask; };

        // Act
        await tester.TestEvent<InstanceHealthInfo, ClusterInformationConsumer>(healthEvent);

        // Assert
        instanceId.Should().NotBeNull();
    }
}
